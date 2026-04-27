using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

#nullable disable

namespace OLEDSaver
{
    static partial class Program
    {
        private const int WM_INPUT = 0x00FF;
        private const uint RID_INPUT = 0x10000003;
        private const uint RIM_TYPEHID = 2;
        private const uint RIDEV_INPUTSINK = 0x00000100;
        private const uint RIDEV_DEVNOTIFY = 0x00002000;
        private const uint ERROR_SUCCESS = 0;
        private const uint ERROR_DEVICE_NOT_CONNECTED = 1167;
        private const int XInputControllerCount = 4;
        private const int XInputGamepadUsagePage = 0x01;
        private const int XInputJoystickUsage = 0x04;
        private const int XInputGamepadUsage = 0x05;
        private const int XInputMultiAxisUsage = 0x08;
        private const byte XInputTriggerThreshold = 30;
        private const short XInputLeftThumbDeadzone = 7849;
        private const short XInputRightThumbDeadzone = 8689;

        private static readonly uint[] _xInputPacketNumbers = new uint[XInputControllerCount];
        private static readonly bool[] _xInputControllersConnected = new bool[XInputControllerCount];
        private static DateTime _lastControllerActivityTimeUtc = DateTime.MinValue;
        private static ControllerRawInputWindow _controllerRawInputWindow;

        private static void SetupControllerActivityTracking()
        {
            if (_controllerRawInputWindow != null)
            {
                return;
            }

            _controllerRawInputWindow = new ControllerRawInputWindow();
            _controllerRawInputWindow.RegisterForControllerInput();
        }

        private static void StopControllerActivityTracking()
        {
            _controllerRawInputWindow?.Dispose();
            _controllerRawInputWindow = null;
        }

        private static double GetControllerIdleTimeSeconds()
        {
            PollXInputControllers();

            if (_lastControllerActivityTimeUtc == DateTime.MinValue)
            {
                return double.PositiveInfinity;
            }

            double idleSeconds = (DateTime.UtcNow - _lastControllerActivityTimeUtc).TotalSeconds;
            return idleSeconds < 0 ? 0 : idleSeconds;
        }

        private static void RecordControllerActivity()
        {
            _lastControllerActivityTimeUtc = DateTime.UtcNow;
        }

        private static void PollXInputControllers()
        {
            for (uint userIndex = 0; userIndex < XInputControllerCount; userIndex++)
            {
                uint result = XInputGetState(userIndex, out XINPUT_STATE state);
                int slot = (int)userIndex;

                if (result != ERROR_SUCCESS)
                {
                    if (result == ERROR_DEVICE_NOT_CONNECTED)
                    {
                        _xInputControllersConnected[slot] = false;
                        _xInputPacketNumbers[slot] = 0;
                    }

                    continue;
                }

                bool isActive = IsXInputStateActive(state.Gamepad);

                if (!_xInputControllersConnected[slot])
                {
                    _xInputControllersConnected[slot] = true;
                    _xInputPacketNumbers[slot] = state.dwPacketNumber;

                    if (isActive)
                    {
                        RecordControllerActivity();
                    }

                    continue;
                }

                if (state.dwPacketNumber != _xInputPacketNumbers[slot])
                {
                    _xInputPacketNumbers[slot] = state.dwPacketNumber;
                    RecordControllerActivity();
                    continue;
                }

                if (isActive)
                {
                    RecordControllerActivity();
                }
            }
        }

        private static bool IsXInputStateActive(XINPUT_GAMEPAD gamepad)
        {
            return gamepad.wButtons != 0 ||
                   gamepad.bLeftTrigger > XInputTriggerThreshold ||
                   gamepad.bRightTrigger > XInputTriggerThreshold ||
                   Math.Abs(gamepad.sThumbLX) > XInputLeftThumbDeadzone ||
                   Math.Abs(gamepad.sThumbLY) > XInputLeftThumbDeadzone ||
                   Math.Abs(gamepad.sThumbRX) > XInputRightThumbDeadzone ||
                   Math.Abs(gamepad.sThumbRY) > XInputRightThumbDeadzone;
        }

        private sealed class ControllerRawInputWindow : NativeWindow, IDisposable
        {
            public ControllerRawInputWindow()
            {
                CreateHandle(new CreateParams());
            }

            public void RegisterForControllerInput()
            {
                RAWINPUTDEVICE[] devices =
                {
                    CreateControllerDeviceRegistration(XInputJoystickUsage),
                    CreateControllerDeviceRegistration(XInputGamepadUsage),
                    CreateControllerDeviceRegistration(XInputMultiAxisUsage)
                };

                try
                {
                    RegisterRawInputDevices(
                        devices,
                        (uint)devices.Length,
                        (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
                }
                catch
                {
                }
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_INPUT)
                {
                    ProcessControllerInput(m.LParam);
                }

                base.WndProc(ref m);
            }

            public void Dispose()
            {
                if (Handle != IntPtr.Zero)
                {
                    DestroyHandle();
                }
            }

            private RAWINPUTDEVICE CreateControllerDeviceRegistration(ushort usage)
            {
                return new RAWINPUTDEVICE
                {
                    usUsagePage = XInputGamepadUsagePage,
                    usUsage = usage,
                    dwFlags = RIDEV_INPUTSINK | RIDEV_DEVNOTIFY,
                    hwndTarget = Handle
                };
            }

            private static void ProcessControllerInput(IntPtr rawInputHandle)
            {
                uint rawInputSize = 0;
                uint headerSize = (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER));

                if (GetRawInputData(rawInputHandle, RID_INPUT, IntPtr.Zero, ref rawInputSize, headerSize) == uint.MaxValue ||
                    rawInputSize < headerSize)
                {
                    return;
                }

                IntPtr buffer = Marshal.AllocHGlobal((int)rawInputSize);

                try
                {
                    if (GetRawInputData(rawInputHandle, RID_INPUT, buffer, ref rawInputSize, headerSize) == uint.MaxValue)
                    {
                        return;
                    }

                    RAWINPUTHEADER header = Marshal.PtrToStructure<RAWINPUTHEADER>(buffer);
                    if (header.dwType == RIM_TYPEHID)
                    {
                        RecordControllerActivity();
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_GAMEPAD
        {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_STATE
        {
            public uint dwPacketNumber;
            public XINPUT_GAMEPAD Gamepad;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(
            RAWINPUTDEVICE[] pRawInputDevices,
            uint uiNumDevices,
            uint cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputData(
            IntPtr hRawInput,
            uint uiCommand,
            IntPtr pData,
            ref uint pcbSize,
            uint cbSizeHeader);

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern uint XInputGetState(uint dwUserIndex, out XINPUT_STATE pState);
    }
}
