using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;
using File = System.IO.File;

#nullable disable

namespace OLEDSaver
{
    static partial class Program
    {
        private static readonly Dictionary<Screen, OverlayForm> _blackOverlays = new Dictionary<Screen, OverlayForm>();
        private static readonly Dictionary<OverlayForm, OverlayRegionState> _overlayRegionStates = new Dictionary<OverlayForm, OverlayRegionState>();

        private static void SetupOverlayWindows()
        {
            foreach (var screen in GetScreens())
            {
                var overlay = new OverlayForm();
                overlay.BackColor = Color.Black;
                overlay.FormBorderStyle = FormBorderStyle.None;
                overlay.Bounds = screen.Bounds;
                overlay.TopMost = true;
                overlay.ShowInTaskbar = false;
                overlay.StartPosition = FormStartPosition.Manual;
                overlay.Opacity = 1.0;
                overlay.KeyPreview = true;
                overlay.ClickThrough = false;
                overlay.Click += (s, e) => HideOverlays();
                overlay.KeyDown += (s, e) => HideOverlays();

                overlay.MouseEnter += (s, e) => RefreshOverlayCursorVisibility();
                overlay.MouseLeave += (s, e) => RefreshOverlayCursorVisibility();

                overlay.VisibleChanged += (s, e) =>
                {
                    RefreshOverlayCursorVisibility();
                };

                _overlays[screen] = overlay;
            }
        }

        private static void StartInactivityTimer()
        {
            _inactivityTimer = new Timer { Interval = DefaultInactivityPollingIntervalMs };
            _inactivityTimer.Tick += (s, e) => CheckInactivity();
            _inactivityTimer.Start();
        }

        private static bool AnyMonitorOverlayEnabled()
        {
            foreach (var setting in _monitorSettings.Values)
            {
                if (setting.Enabled)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool AnyVisibleMonitorOverlay()
        {
            foreach (var overlay in _overlays.Values)
            {
                if (!overlay.IsDisposed && overlay.Visible)
                {
                    return true;
                }
            }

            return false;
        }

        private static void UpdateBackgroundTimerProfile()
        {
            if (_edgeTimer != null)
            {
                if (!_taskbarHidingEnabled)
                {
                    SetEdgeTimerInterval(DisabledEdgePollingIntervalMs);
                }
                else
                {
                    SetEdgeTimerInterval(_taskbarHidden ? IdleEdgePollingIntervalMs : ActiveEdgePollingIntervalMs);
                }
            }

            if (_inactivityTimer != null)
            {
                bool hasBackgroundFeatures =
                    AnyMonitorOverlayEnabled() ||
                    _drawBlackOverlayEnabled ||
                    _desktopIconsHidingEnabled ||
                    _screenOffEnabled;

                int targetInterval = hasBackgroundFeatures
                    ? DefaultInactivityPollingIntervalMs
                    : QuiescentInactivityPollingIntervalMs;

                if (_inactivityTimer.Interval != targetInterval)
                {
                    _inactivityTimer.Interval = targetInterval;
                }
            }
        }

        private static void CheckInactivity()
        {
            double idleSeconds = GetIdleTimeSeconds();
            bool desktopIconsHidingEnabled = _desktopIconsHidingEnabled;
            bool drawBlackOverlayEnabled = _drawBlackOverlayEnabled;
            bool screenOffEnabled = _screenOffEnabled;
            bool anyMonitorOverlayEnabled = AnyMonitorOverlayEnabled();

            if (_screenOff && idleSeconds < 1.0)
            {
                TurnOnDisplay();
            }

            _isVideoPlaying = ShouldEvaluateVideoState(idleSeconds) && IsVideoPlaying();

            if (!anyMonitorOverlayEnabled)
            {
                if (AnyVisibleMonitorOverlay())
                {
                    HideOverlays();
                }
            }
            else
            {
                foreach (var kvp in _overlays)
                {
                    var screen = kvp.Key;
                    var overlay = kvp.Value;
                    var setting = _monitorSettings[screen.DeviceName];
                    if (setting.Enabled && idleSeconds >= setting.TimeoutSeconds && !_isVideoPlaying)
                    {
                        if (!overlay.Visible)
                            overlay.Show();
                    }
                    else
                    {
                        if (overlay.Visible)
                            overlay.Hide();
                    }
                }
            }

            if (_drawBlackOverlay && (_overlayUpdateCts == null || _overlayUpdateCts.IsCancellationRequested))
            {
                UpdateBlackOverlays();
            }

            if (desktopIconsHidingEnabled)
            {
                if (idleSeconds >= _desktopIconsTimeoutSeconds)
                {
                    HideDesktopIcons();
                }
                else
                {
                    CheckDesktopInteraction();
                }
            }

            if (drawBlackOverlayEnabled && !_isVideoPlaying)
            {
                if (idleSeconds >= _drawBlackOverlayEnabledTimeoutSeconds)
                {
                    UpdateOverlayForActiveWindow();
                    CheckCursorForOverlayFade();
                }
                else
                {
                    if (_drawBlackOverlay)
                    {
                        _overlayUpdateCts?.Cancel();
                        _overlayUpdateCts = null;
                        _drawBlackOverlay = false;
                        _windowThatTriggeredOverlay = IntPtr.Zero;
                        _overlayFaded = false;
                        HideBlackOverlays();
                    }
                }
            }
            else if (_drawBlackOverlay)
            {
                _overlayUpdateCts?.Cancel();
                _overlayUpdateCts = null;
                _drawBlackOverlay = false;
                _windowThatTriggeredOverlay = IntPtr.Zero;
                _overlayFaded = false;
                HideBlackOverlays();
            }

            if (InactivityDecisions.ShouldTurnOffDisplay(
                screenOffEnabled,
                _screenOff,
                idleSeconds,
                _displayOffTimeoutSeconds,
                _isVideoPlaying))
            {
                TurnOffDisplay();
            }
        }

        private static bool GetExtendedFrameBounds(IntPtr hwnd, out RECT rect)
        {
            rect = new RECT();
            int hr = DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out rect, Marshal.SizeOf(typeof(RECT)));
            return hr == 0;
        }

        private static void UpdateBlackOverlays()
        {
            if (!_drawBlackOverlay || _windowThatTriggeredOverlay == IntPtr.Zero)
                return;

            if (!GetExtendedFrameBounds(_windowThatTriggeredOverlay, out RECT windowRect))
            {
                if (!GetWindowRect(_windowThatTriggeredOverlay, out windowRect))
                    return;
            }

            _targetActiveWindowRect = new Rectangle(
                windowRect.Left,
                windowRect.Top,
                windowRect.Right - windowRect.Left,
                windowRect.Bottom - windowRect.Top);

            bool windowIsMoving = !_targetActiveWindowRect.Equals(_previousTargetRect);
            _previousTargetRect = _targetActiveWindowRect;

            Rectangle newOverlayRect;

            if (_enableInterpolation && !windowIsMoving)
            {
                newOverlayRect = LerpRect(_currentActiveWindowRect, _targetActiveWindowRect, 0.2f);
                if (AreRectanglesClose(newOverlayRect, _targetActiveWindowRect))
                {
                    newOverlayRect = _targetActiveWindowRect;
                }
            }
            else
            {
                newOverlayRect = _targetActiveWindowRect;
            }

            _lastRenderedOverlayRect = newOverlayRect;
            _currentActiveWindowRect = newOverlayRect;

            foreach (var kvp in _blackOverlays)
            {
                var screen = kvp.Key;
                var overlay = kvp.Value;
                Rectangle screenBounds = screen.Bounds;

                Rectangle relativeRect = new Rectangle(
                    newOverlayRect.Left - screenBounds.Left,
                    newOverlayRect.Top - screenBounds.Top,
                    newOverlayRect.Width,
                    newOverlayRect.Height);

                var nextState = OverlayRegionState.Create(relativeRect, overlay.Size, _overlayRoundedCorners);
                if (_overlayRegionStates.TryGetValue(overlay, out OverlayRegionState previousState) &&
                    previousState.Equals(nextState))
                {
                    continue;
                }

                Region nextRegion = new Region(new Rectangle(0, 0, overlay.Width, overlay.Height));

                if (!nextState.CutoutRect.IsEmpty && nextState.RoundedCorners)
                {
                    using Region roundedWindowRegion = CreateRoundedRegion(nextState.CutoutRect, 6);
                    nextRegion.Exclude(roundedWindowRegion);
                }
                else if (!nextState.CutoutRect.IsEmpty)
                {
                    using Region rectangularWindowRegion = new Region(nextState.CutoutRect);
                    nextRegion.Exclude(rectangularWindowRegion);
                }

                Region previousRegion = overlay.Region;
                overlay.Region = nextRegion;
                previousRegion?.Dispose();
                _overlayRegionStates[overlay] = nextState;
            }
        }

        private static Rectangle LerpRect(Rectangle from, Rectangle to, float t)
        {
            return new Rectangle(
                (int)(from.X + (to.X - from.X) * t),
                (int)(from.Y + (to.Y - from.Y) * t),
                (int)(from.Width + (to.Width - from.Width) * t),
                (int)(from.Height + (to.Height - from.Height) * t)
            );
        }

        private static bool AreRectanglesClose(Rectangle left, Rectangle right, int tolerance = 1)
        {
            return Math.Abs(left.X - right.X) <= tolerance &&
                   Math.Abs(left.Y - right.Y) <= tolerance &&
                   Math.Abs(left.Width - right.Width) <= tolerance &&
                   Math.Abs(left.Height - right.Height) <= tolerance;
        }

        private static void CheckCursorForOverlayFade()
        {
            if (!_drawBlackOverlay || _windowThatTriggeredOverlay == IntPtr.Zero)
                return;

            if (!GetExtendedFrameBounds(_windowThatTriggeredOverlay, out RECT rect))
                return;

            Point cursor;
            if (!GetCursorPos(out cursor)) return;

            bool cursorInside = cursor.X >= rect.Left &&
                                cursor.X <= rect.Right &&
                                cursor.Y >= rect.Top &&
                                cursor.Y <= rect.Bottom;

            if (!cursorInside && !_overlayFaded)
            {
                foreach (var overlay in _blackOverlays.Values)
                {
                    if (!overlay.IsDisposed)
                        overlay.Opacity = _overlayFadedOpacity;
                }
                _overlayFaded = true;
            }
            else if (cursorInside && _overlayFaded)
            {
                foreach (var overlay in _blackOverlays.Values)
                {
                    if (!overlay.IsDisposed)
                        overlay.Opacity = _overlayOpacity;
                }
                _overlayFaded = false;
            }
        }

        private static int GetScreenRefreshRate(Screen screen)
        {
            DEVMODE devMode = new DEVMODE();
            devMode.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));

            if (EnumDisplaySettings(screen.DeviceName, ENUM_CURRENT_SETTINGS, ref devMode))
            {
                int refreshRate = (int)devMode.dmDisplayFrequency;
                if (refreshRate > 0)
                    return refreshRate;
            }

            return 60;
        }

        private static async Task StartOverlayRealtimeUpdates()
        {
            _overlayUpdateCts?.Cancel();
            _overlayUpdateCts = new CancellationTokenSource();
            var token = _overlayUpdateCts.Token;

            int refreshRate = Math.Max(1, Math.Min(MaxOverlayRefreshRate, GetScreenRefreshRate(GetPrimaryScreen())));
            int fastDelay = Math.Max(16, 1000 / refreshRate);
            int slowDelay = OverlaySlowPollIntervalMs;

            Point lastCursor = new Point();
            _lastWindowBounds = Rectangle.Empty;

            int activityFrames = 0;
            int maxActivityFrames = Math.Max(12, refreshRate / 3);

            try
            {
                while (!token.IsCancellationRequested)
                {
                    bool updated = false;

                    IntPtr currentForeground = GetForegroundWindow();
                    if (currentForeground == _windowThatTriggeredOverlay)
                    {
                        if (GetWindowRect(currentForeground, out RECT currentRect))
                        {
                            Rectangle currentBounds = new Rectangle(
                                currentRect.Left,
                                currentRect.Top,
                                currentRect.Right - currentRect.Left,
                                currentRect.Bottom - currentRect.Top);

                            if (!_lastWindowBounds.Equals(currentBounds))
                            {
                                UpdateBlackOverlays();
                                _lastWindowBounds = currentBounds;
                                updated = true;
                            }
                        }
                    }
                    else
                    {
                        UpdateOverlayForActiveWindow();
                        updated = true;
                    }

                    Point currentCursor;
                    if (_drawBlackOverlay && GetCursorPos(out currentCursor) && currentCursor != lastCursor)
                    {
                        bool wasInside = IsCursorInsideWindow(_windowThatTriggeredOverlay, lastCursor);
                        bool isNowInside = IsCursorInsideWindow(_windowThatTriggeredOverlay, currentCursor);

                        if (wasInside != isNowInside)
                        {
                            CheckCursorForOverlayFade();
                            updated = true;
                        }

                        lastCursor = currentCursor;
                    }

                    if (updated)
                    {
                        activityFrames = maxActivityFrames;
                        await Task.Delay(fastDelay, token);
                    }
                    else if (activityFrames > 0)
                    {
                        activityFrames--;
                        await Task.Delay(fastDelay, token);
                    }
                    else
                    {
                        await Task.Delay(slowDelay, token);
                    }
                }
            }
            catch (TaskCanceledException)
            {
            }
        }

        private static bool IsCursorInsideWindow(IntPtr hwnd, Point cursor)
        {
            if (!GetExtendedFrameBounds(hwnd, out RECT rect))
                return false;

            return cursor.X >= rect.Left &&
                   cursor.X <= rect.Right &&
                   cursor.Y >= rect.Top &&
                   cursor.Y <= rect.Bottom;
        }

        private static Region CreateRoundedRegion(Rectangle bounds, int radius)
        {
            using GraphicsPath path = new GraphicsPath();

            int diameter = radius * 2;
            Size size = new Size(diameter, diameter);
            Rectangle arc = new Rectangle(bounds.Location, size);

            path.AddArc(arc, 180, 90);

            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);

            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return new Region(path);
        }

        private static void RefreshOverlayCursorVisibility()
        {
            bool shouldHideCursor = false;
            foreach (var overlay in _overlays.Values)
            {
                if (!overlay.IsDisposed && overlay.Visible)
                {
                    shouldHideCursor = true;
                    break;
                }
            }

            if (shouldHideCursor == _cursorHiddenByOverlays)
            {
                return;
            }

            if (shouldHideCursor)
                Cursor.Hide();
            else
                Cursor.Show();

            _cursorHiddenByOverlays = shouldHideCursor;
        }

        private static string GetWindowClassName(IntPtr hWnd)
        {
            var className = new StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);
            return className.ToString();
        }

        private static void CreateOrUpdateBlackOverlays()
        {
            if (_windowThatTriggeredOverlay == IntPtr.Zero) return;

            if (!GetWindowRect(_windowThatTriggeredOverlay, out RECT activeWindowRect)) return;

            foreach (var screen in GetScreens())
            {
                Rectangle screenBounds = screen.Bounds;

                OverlayForm overlay;
                if (!_blackOverlays.TryGetValue(screen, out overlay) || overlay.IsDisposed)
                {
                    overlay = new OverlayForm();
                    overlay.FormBorderStyle = FormBorderStyle.None;
                    overlay.ShowInTaskbar = false;
                    overlay.StartPosition = FormStartPosition.Manual;
                    overlay.TopMost = true;
                    overlay.BackColor = Color.Black;
                    overlay.Opacity = _overlayOpacity;
                    overlay.Bounds = screenBounds;
                    overlay.ClickThrough = true;
                    overlay.Show();
                    _blackOverlays[screen] = overlay;
                }
                else
                {
                    overlay.Bounds = screenBounds;
                    overlay.Opacity = _overlayOpacity;
                    overlay.Show();
                }
            }

            _lastActiveWindowRect = Rectangle.Empty;
            UpdateBlackOverlays();
        }

        private static void UpdateOverlayForActiveWindow()
        {
            if (!_drawBlackOverlayEnabled)
                return;

            IntPtr currentForeground = GetForegroundWindow();
            if (currentForeground == IntPtr.Zero)
            {
                HideBlackOverlays();
                _windowThatTriggeredOverlay = IntPtr.Zero;
                _drawBlackOverlay = false;
                return;
            }

            if (currentForeground != _windowThatTriggeredOverlay)
            {
                _windowThatTriggeredOverlay = currentForeground;

                if (!_drawBlackOverlay)
                {
                    CreateOrUpdateBlackOverlays();
                    _ = StartOverlayRealtimeUpdates();
                    _drawBlackOverlay = true;
                }
                else
                {
                    UpdateBlackOverlays();
                }
            }
            else if (_drawBlackOverlay)
            {
                UpdateBlackOverlays();
            }
        }

        public class OverlayForm : Form
        {
            private bool _clickThrough = false;

            [Browsable(false)]
            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            public bool ClickThrough
            {
                get => _clickThrough;
                set
                {
                    if (_clickThrough != value)
                    {
                        _clickThrough = value;
                        UpdateWindowStyle();
                    }
                }
            }

            protected override CreateParams CreateParams
            {
                get
                {
                    CreateParams cp = base.CreateParams;
                    cp.ExStyle |= 0x80;
                    cp.ExStyle |= 0x08000000;

                    if (_clickThrough)
                    {
                        cp.ExStyle |= 0x20;
                    }

                    return cp;
                }
            }

            protected override bool ShowWithoutActivation => true;

            private void UpdateWindowStyle()
            {
                if (IsHandleCreated)
                {
                    int exStyle = GetWindowLong(Handle, -20);
                    if (_clickThrough)
                        exStyle |= 0x20;
                    else
                        exStyle &= ~0x20;

                    SetWindowLong(Handle, -20, exStyle);
                }
            }
        }

        private static void HideBlackOverlays()
        {
            foreach (var overlay in _blackOverlays.Values)
            {
                if (!overlay.IsDisposed)
                {
                    overlay.Hide();
                    overlay.Region?.Dispose();
                    overlay.Region = null;
                    _overlayRegionStates.Remove(overlay);
                }
            }

            _lastRenderedOverlayRect = Rectangle.Empty;
        }

        private static double GetIdleTimeSeconds()
        {
            double systemIdleSeconds = GetSystemIdleTimeSeconds();
            double controllerIdleSeconds = GetControllerIdleTimeSeconds();
            return Math.Min(systemIdleSeconds, controllerIdleSeconds);
        }

        private static double GetSystemIdleTimeSeconds()
        {
            LASTINPUTINFO info = new LASTINPUTINFO();
            info.cbSize = (uint)Marshal.SizeOf(info);
            if (GetLastInputInfo(ref info))
            {
                uint tickCount = (uint)Environment.TickCount;
                uint lastInput = info.dwTime;
                uint diff = tickCount - lastInput;
                return diff / 1000.0;
            }
            return 0;
        }

        private static void HideOverlays()
        {
            foreach (var overlay in _overlays.Values)
            {
                if (overlay.Visible)
                    overlay.Hide();
            }

            RefreshOverlayCursorVisibility();
        }

        private static void TurnOffDisplay()
        {
            if (_screenOff) return;

            SendMessage(HWND_BROADCAST, WM_SYSCOMMAND, (IntPtr)SC_MONITORPOWER, (IntPtr)2);
            _screenOff = true;
        }

        private static void TurnOnDisplay()
        {
            if (!_screenOff) return;
            SendMessage(HWND_BROADCAST, WM_SYSCOMMAND, (IntPtr)SC_MONITORPOWER, (IntPtr)(-1));
            _screenOff = false;
        }

        private static void ExitApplication()
        {
            SaveSettings();
            StopDesktopMonitoring();
            StopScreenSnapshotInvalidation();
            StopControllerActivityTracking();
            _edgeTimer?.Stop();
            _edgeTimer?.Dispose();
            _edgeTimer = null;

            if (_desktopIconsHidden)
                ShowDesktopIconsIfNeeded();

            if (_taskbarHidden)
                ShowTaskbarAndDesktop();

            if (_screenOff)
                TurnOnDisplay();

            _overlayUpdateCts?.Cancel();

            _trayIcon?.Dispose();
            foreach (var overlay in _overlays.Values)
                overlay?.Dispose();

            foreach (var overlay in _blackOverlays.Values)
                overlay?.Dispose();

            _overlayRegionStates.Clear();

            Application.Exit();
        }
    }
}
