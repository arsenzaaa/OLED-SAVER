using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

#nullable disable

namespace OLEDSaver
{
    static partial class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            StartScreenSnapshotInvalidation();
            InitializeMonitorSettings();
            LoadSettings();
            SetupControllerActivityTracking();
            if (_desktopIconsHidingEnabled)
            {
                StartDesktopMonitoring();
            }
            MagicTrick();
            SetupTrayIcon();
            SetupOverlayWindows();
            StartInactivityTimer();
            StartEdgeTimer();
            UpdateBackgroundTimerProfile();

            Application.Run();
        }

        static void MagicTrick()
        {
            ApplyLowPriorityProfile();

            var currentProcess = Process.GetCurrentProcess();
            foreach (ProcessThread thread in currentProcess.Threads)
            {
                try
                {
                    uint access = THREAD_SET_INFORMATION | THREAD_QUERY_INFORMATION;
                    IntPtr hThread = OpenThread(access, false, (uint)thread.Id);
                    if (hThread != IntPtr.Zero)
                    {
                        SetThreadPriority(hThread, THREAD_PRIORITY_IDLE);
                        SetThreadPriorityBoost(hThread, true);
                        CloseHandle(hThread);
                    }
                }
                catch
                {
                }
            }
        }

        private static void ApplyLowPriorityProfile()
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                currentProcess.PriorityClass = ProcessPriorityClass.Idle;
                SetProcessPriorityBoost(currentProcess.Handle, true);
            }
            catch
            {
            }

            try
            {
                IntPtr currentThread = GetCurrentThread();
                if (currentThread != IntPtr.Zero)
                {
                    SetThreadPriority(currentThread, THREAD_PRIORITY_IDLE);
                    SetThreadPriorityBoost(currentThread, true);
                }
            }
            catch
            {
            }
        }

        private static void StartEdgeTimer()
        {
            _edgeTimer = new Timer { Interval = TaskbarStatePollingIntervalMs };
            _edgeTimer.Tick += (s, e) =>
            {
                if (!_taskbarHidingEnabled)
                {
                    SetEdgeTimerInterval(DisabledEdgePollingIntervalMs);
                    return;
                }

                if (IsWindowsKeyPressed())
                {
                    SetEdgeTimerInterval(ActiveEdgePollingIntervalMs);
                    _lastWindowsKeyTime = DateTime.Now;
                    ShowTaskbarAndDesktop();
                    _lastTaskbarInteractionTime = DateTime.Now;
                    return;
                }

                if ((DateTime.Now - _lastWindowsKeyTime) < _winKeyShowDuration)
                {
                    SetEdgeTimerInterval(ActiveEdgePollingIntervalMs);
                    ShowTaskbarAndDesktop();
                    _lastTaskbarInteractionTime = DateTime.Now;
                    return;
                }

                SetEdgeTimerInterval(_taskbarHidden ? IdleEdgePollingIntervalMs : ActiveEdgePollingIntervalMs);

                if (GetCursorPos(out Point cursorPosition))
                {
                    HandleTaskbarEdgePointerActivity(cursorPosition);
                }

                if (!_taskbarHidden)
                {
                    if (_lastTaskbarInteractionTime == DateTime.MinValue)
                    {
                        _lastTaskbarInteractionTime = DateTime.Now;
                    }

                    var elapsed = DateTime.Now - _lastTaskbarInteractionTime;
                    if (elapsed.TotalSeconds >= _taskbarTimeoutSeconds)
                    {
                        HideTaskbarAndDesktop();
                        _lastTaskbarInteractionTime = DateTime.MinValue;
                    }
                }
            };
            _edgeTimer.Start();
        }

        private static bool IsNearTaskbarEdge(Point cursorPosition)
        {
            return cursorPosition.Y >= GetPrimaryScreen().Bounds.Bottom - _activityThreshold;
        }

        private static void HandleTaskbarEdgePointerActivity(Point cursorPosition)
        {
            if (!_taskbarHidingEnabled || !IsNearTaskbarEdge(cursorPosition))
            {
                return;
            }

            ShowTaskbarAndDesktop();
            _lastTaskbarInteractionTime = DateTime.Now;
        }

        private static void SetEdgeTimerInterval(int interval)
        {
            if (_edgeTimer != null && _edgeTimer.Interval != interval)
            {
                _edgeTimer.Interval = interval;
            }
        }

        private static void InitializeMonitorSettings()
        {
            var screens = GetScreens();
            var primaryScreen = GetPrimaryScreen();

            for (int i = 0; i < screens.Length; i++)
            {
                var screen = screens[i];
                var displayName = $"Monitor {i + 1}";
                if (screen == primaryScreen)
                    displayName += " (Primary)";

                _monitorSettings[screen.DeviceName] = new MonitorSettings
                {
                    DeviceName = screen.DeviceName,
                    DisplayName = displayName,
                    Enabled = true,
                    TimeoutSeconds = 25
                };
            }
        }
    }
}
