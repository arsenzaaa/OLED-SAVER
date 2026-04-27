using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

#nullable disable

namespace OLEDSaver
{
    static partial class Program
    {
        private static readonly CachedWindowHandles _extraTaskbarWindows = new CachedWindowHandles();

        private static void CheckDesktopInteraction()
        {
            if (!GetCursorPos(out Point currentCursorPos)) return;

            bool isOverDesktop = IsUserInteractingWithDesktop(currentCursorPos);

            if (isOverDesktop)
            {
                if (_lastCursorPos != currentCursorPos)
                {
                    _lastActiveTime = DateTime.Now;
                    _lastCursorPos = currentCursorPos;
                }

                var inactiveTime = DateTime.Now - _lastActiveTime;
                if (inactiveTime >= InactivityThreshold)
                {
                    if (!_desktopIconsHidden)
                        HideDesktopIcons();
                }
                else
                {
                    if (_desktopIconsHidden)
                        ShowDesktopIcons();
                }
            }
            else
            {
                _lastActiveTime = DateTime.Now;
                if (!_desktopIconsHidden)
                    HideDesktopIcons();
            }
        }

        private static bool IsUserInteractingWithDesktop(Point cursorPos)
        {
            var windowUnderCursor = WindowFromPoint(cursorPos);
            if (windowUnderCursor == IntPtr.Zero) return false;

            var rootWindow = GetAncestor(windowUnderCursor, GA_ROOT);

            var className = GetWindowClassName(rootWindow);

            return IsDesktopRelatedWindow(className, rootWindow);
        }

        private static bool IsDesktopRelatedWindow(string className, IntPtr hWnd)
        {
            return DesktopWindowClasses.Contains(className);
        }

        private static void HideStartButtonsWithStartAllBack()
        {
            if (_extraTaskbarWindows.TryGetValid(IsWindow, out var cachedHandles))
            {
                foreach (var hWnd in cachedHandles)
                {
                    ShowWindow(hWnd, SW_HIDE);
                }

                return;
            }

            var discoveredWindows = new List<IntPtr>();
            Rectangle primaryBounds = GetPrimaryScreen().Bounds;

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd))
                    return true;

                if (!GetWindowRect(hWnd, out RECT r))
                    return true;

                int width = r.Right - r.Left;
                int height = r.Bottom - r.Top;

                if (width < 24 || width > 128 || height < 24 || height > 128)
                    return true;

                if (r.Bottom < primaryBounds.Bottom - 5)
                    return true;

                string cls = GetWindowClassName(hWnd);
                if (cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd")
                    return true;

                uint pid;
                GetWindowThreadProcessId(hWnd, out pid);

                if (!string.Equals(GetProcessName(pid), "explorer", StringComparison.OrdinalIgnoreCase))
                    return true;

                discoveredWindows.Add(hWnd);
                ShowWindow(hWnd, SW_HIDE);
                return true;
            }, IntPtr.Zero);

            _extraTaskbarWindows.Replace(discoveredWindows);
        }

        private static void ShowStartButtonsWithStartAllBack()
        {
            if (!_extraTaskbarWindows.TryGetValid(IsWindow, out var cachedHandles))
            {
                return;
            }

            foreach (var hWnd in cachedHandles)
            {
                ShowWindow(hWnd, SW_SHOW);
            }
        }

        private static void HideTaskbarAndDesktop()
        {
            if (_taskbarHidden) return;

            var taskbar = FindWindow("Shell_TrayWnd", null);
            if (taskbar == IntPtr.Zero) return;

            if (!_workAreaStored)
            {
                SystemParametersInfo(SPI_GETWORKAREA, 0, ref _originalWorkArea, 0);
                _workAreaStored = true;
            }

            ShowWindow(taskbar, SW_HIDE);
            HideStartButtonsWithStartAllBack();

            RECT unchanged = _originalWorkArea;
            SystemParametersInfo(SPI_SETWORKAREA, 0, ref unchanged, SPIF_SENDCHANGE);

            _taskbarHidden = true;
        }

        private static void ShowTaskbarAndDesktop()
        {
            if (!_taskbarHidden) return;

            var taskbar = FindWindow("Shell_TrayWnd", null);
            if (taskbar == IntPtr.Zero) return;

            ShowWindow(taskbar, SW_SHOW);
            ShowStartButtonsWithStartAllBack();

            if (_workAreaStored)
                SystemParametersInfo(SPI_SETWORKAREA, 0, ref _originalWorkArea, SPIF_SENDCHANGE);

            _taskbarHidden = false;
        }

        private static void HideDesktopIcons()
        {
            if (_desktopIconsHidden || !IsDesktopVisible()) return;
            var hWndDesktop = GetDesktopListView();
            if (hWndDesktop != IntPtr.Zero)
            {
                ShowWindow(hWndDesktop, SW_HIDE);
                _desktopIconsHidden = true;
            }
        }

        private static void ShowDesktopIcons()
        {
            if (!_desktopIconsHidden) return;

            var hWndDesktop = GetDesktopListView();
            if (hWndDesktop != IntPtr.Zero)
            {
                ShowWindow(hWndDesktop, SW_SHOW);
                UpdateWindow(hWndDesktop);
                RedrawWindow(hWndDesktop, IntPtr.Zero, IntPtr.Zero, RDW_INVALIDATE | RDW_UPDATENOW | RDW_ALLCHILDREN);
                _desktopIconsHidden = false;
            }
        }

        private static IntPtr GetDesktopListView()
        {
            IntPtr hShellViewWin = IntPtr.Zero;
            IntPtr hProgman = FindWindow("Progman", null);
            if (hProgman != IntPtr.Zero)
            {
                IntPtr hDesktopWnd = FindWindowEx(hProgman, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (hDesktopWnd == IntPtr.Zero)
                {
                    IntPtr hWorkerW = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "WorkerW", null);
                    while (hWorkerW != IntPtr.Zero && hDesktopWnd == IntPtr.Zero)
                    {
                        hDesktopWnd = FindWindowEx(hWorkerW, IntPtr.Zero, "SHELLDLL_DefView", null);
                        hWorkerW = FindWindowEx(IntPtr.Zero, hWorkerW, "WorkerW", null);
                    }
                }
                if (hDesktopWnd != IntPtr.Zero)
                {
                    hShellViewWin = FindWindowEx(hDesktopWnd, IntPtr.Zero, "SysListView32", "FolderView");
                }
            }
            return hShellViewWin;
        }

        private static bool IsDesktopVisible()
        {
            var foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero) return true;

            var className = GetWindowClassName(foregroundWindow);
            if (className == "Progman")
            {
                return true;
            }

            if (GetWindowRect(foregroundWindow, out RECT windowRect))
            {
                var screenWidth = GetSystemMetrics(SM_CXSCREEN);
                var screenHeight = GetSystemMetrics(SM_CYSCREEN);

                bool isFullscreen = windowRect.Left <= 0 &&
                                    windowRect.Top <= 0 &&
                                    windowRect.Right >= screenWidth &&
                                    windowRect.Bottom >= screenHeight;

                return !isFullscreen;
            }
            return true;
        }

        private static void HideDesktopIconsIfNeeded()
        {
            if (_desktopIconsHidden) return;

            if (IsDesktopVisible())
            {
                var hWndDesktop = GetDesktopListView();
                if (hWndDesktop != IntPtr.Zero)
                {
                    ShowWindow(hWndDesktop, SW_HIDE);
                    _desktopIconsHidden = true;
                }
            }
        }

        private static void ShowDesktopIconsIfNeeded()
        {
            if (!_desktopIconsHidden) return;

            var hWndDesktop = GetDesktopListView();
            if (hWndDesktop != IntPtr.Zero)
            {
                ShowWindow(hWndDesktop, SW_SHOW);
                _desktopIconsHidden = false;
            }
        }

        private static bool HasVisibleWindows()
        {
            bool hasVisibleWindows = false;

            EnumWindows((hWnd, lParam) =>
            {
                if (IsWindowVisible(hWnd) && !IsIconic(hWnd))
                {
                    if (GetWindowRect(hWnd, out RECT rect))
                    {
                        if (rect.Right - rect.Left > 100 && rect.Bottom - rect.Top > 100)
                        {
                            hasVisibleWindows = true;
                            return false;
                        }
                    }
                }
                return true;
            }, IntPtr.Zero);

            return hasVisibleWindows;
        }

        private static bool ShouldHideDesktopIcons()
        {
            if (!IsDesktopVisible()) return false;

            if (HasVisibleWindows()) return false;

            return true;
        }

        private static void ManageDesktopIcons()
        {
            if (ShouldHideDesktopIcons())
            {
                HideDesktopIconsIfNeeded();
            }
            else
            {
                ShowDesktopIconsIfNeeded();
            }
        }

        private static void StartDesktopMonitoring()
        {
            EnsureDesktopIconRefreshTimer();

            if (_hookHandle != IntPtr.Zero)
                return;

            _hookHandle = SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, _winEventProc, 0, 0,
                WINEVENT_OUTOFCONTEXT);
        }

        private static void StopDesktopMonitoring()
        {
            _desktopIconRefreshTimer?.Stop();
            _desktopIconRefreshTimer?.Dispose();
            _desktopIconRefreshTimer = null;

            if (_hookHandle != IntPtr.Zero)
            {
                UnhookWinEvent(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }
        }

        private static void EnsureDesktopIconRefreshTimer()
        {
            if (_desktopIconRefreshTimer != null)
                return;

            _desktopIconRefreshTimer = new Timer { Interval = DesktopIconRefreshDebounceMs };
            _desktopIconRefreshTimer.Tick += (s, e) =>
            {
                _desktopIconRefreshTimer.Stop();
                ManageDesktopIcons();
            };
        }

        private static void ScheduleDesktopIconRefresh()
        {
            if (!_desktopIconsHidingEnabled)
                return;

            EnsureDesktopIconRefreshTimer();
            _desktopIconRefreshTimer.Stop();
            _desktopIconRefreshTimer.Start();
        }

        private static void WinEventProc(IntPtr hWinEventHook, uint eventType,
            IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (eventType == EVENT_SYSTEM_FOREGROUND)
            {
                ScheduleDesktopIconRefresh();
            }
        }
    }
}
