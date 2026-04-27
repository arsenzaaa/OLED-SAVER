using System;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

#nullable disable

namespace OLEDSaver
{
    static partial class Program
    {
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private static readonly ExpiringValueCache<uint, string> _processNameCache =
            new ExpiringValueCache<uint, string>(TimeSpan.FromSeconds(5), () => DateTime.UtcNow);

        private static bool IsWindowsKeyPressed()
        {
            return (GetAsyncKeyState(VK_LWIN) & 0x8000) != 0 ||
                   (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0;
        }

        private static void InvalidateVideoDetectionCache()
        {
            _lastVideoDetectionWindow = IntPtr.Zero;
            _lastVideoDetectionTime = DateTime.MinValue;
            _lastVideoDetectionResult = false;
        }

        private static string GetProcessName(uint processId)
        {
            if (processId == 0)
            {
                return string.Empty;
            }

            return _processNameCache.GetOrAdd(processId, () =>
            {
                try
                {
                    using (var process = Process.GetProcessById((int)processId))
                    {
                        return process.ProcessName;
                    }
                }
                catch
                {
                    return string.Empty;
                }
            });
        }

        private static bool ShouldEvaluateVideoState(double idleSeconds)
        {
            double earliestThreshold = double.PositiveInfinity;

            foreach (var setting in _monitorSettings.Values)
            {
                if (setting.Enabled)
                {
                    earliestThreshold = Math.Min(earliestThreshold, setting.TimeoutSeconds);
                }
            }

            if (_drawBlackOverlayEnabled)
            {
                earliestThreshold = Math.Min(earliestThreshold, _drawBlackOverlayEnabledTimeoutSeconds);
            }

            if (_screenOffEnabled)
            {
                earliestThreshold = Math.Min(earliestThreshold, _displayOffTimeoutSeconds);
            }

            if (double.IsPositiveInfinity(earliestThreshold))
            {
                return false;
            }

            return idleSeconds >= Math.Max(0, earliestThreshold - 2);
        }

        private static bool ContainsIgnoreCase(string source, string value)
        {
            return !string.IsNullOrEmpty(source) &&
                   !string.IsNullOrEmpty(value) &&
                   source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsAnyIgnoreCase(string source, string[] indicators)
        {
            if (string.IsNullOrEmpty(source))
            {
                return false;
            }

            for (int i = 0; i < indicators.Length; i++)
            {
                if (ContainsIgnoreCase(source, indicators[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsAnyIgnoreCase(string source, List<string> indicators)
        {
            if (string.IsNullOrEmpty(source))
            {
                return false;
            }

            for (int i = 0; i < indicators.Count; i++)
            {
                if (ContainsIgnoreCase(source, indicators[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsVideoPlaying()
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero)
                {
                    InvalidateVideoDetectionCache();
                    return false;
                }

                var now = DateTime.UtcNow;
                if (hwnd == _lastVideoDetectionWindow &&
                    (now - _lastVideoDetectionTime) < VideoDetectionCacheDuration)
                {
                    return _lastVideoDetectionResult;
                }

                StringBuilder className = new StringBuilder(256);
                StringBuilder title = new StringBuilder(256);
                GetClassName(hwnd, className, className.Capacity);
                GetWindowText(hwnd, title, title.Capacity);

                string cls = className.ToString();
                string ttl = title.ToString();
                string processName = string.Empty;

                uint processId;
                GetWindowThreadProcessId(hwnd, out processId);
                processName = GetProcessName(processId);

                if (_excludedWindowTitles.Count > 0)
                {
                    if (ContainsAnyIgnoreCase(ttl, _excludedWindowTitles) ||
                        ContainsAnyIgnoreCase(cls, _excludedWindowTitles) ||
                        ContainsAnyIgnoreCase(processName, _excludedWindowTitles))
                    {
                        _lastVideoDetectionWindow = hwnd;
                        _lastVideoDetectionTime = now;
                        _lastVideoDetectionResult = true;
                        return true;
                    }
                }

                bool matchProcess = false;
                bool matchTitle = false;
                bool matchClass = false;

                for (int i = 0; i < _videoApps.Length; i++)
                {
                    string indicator = _videoApps[i];

                    if (!matchProcess && ContainsIgnoreCase(processName, indicator))
                    {
                        matchProcess = true;
                    }

                    if (!matchTitle && ContainsIgnoreCase(ttl, indicator))
                    {
                        matchTitle = true;
                    }

                    if (!matchClass && ContainsIgnoreCase(cls, indicator))
                    {
                        matchClass = true;
                    }

                    if (matchProcess && matchTitle && matchClass)
                    {
                        break;
                    }
                }

                bool hasPlaybackIndicator = ContainsAnyIgnoreCase(ttl, _videoTitleIndicators);
                bool isVideo = matchProcess || matchTitle || matchClass || hasPlaybackIndicator;

                if (!isVideo && GetWindowRect(hwnd, out RECT rect))
                {
                    Rectangle screenBounds = Screen.FromHandle(hwnd).Bounds;

                    bool isFullscreen = rect.Left <= screenBounds.Left &&
                                        rect.Top <= screenBounds.Top &&
                                        rect.Right >= screenBounds.Right &&
                                        rect.Bottom >= screenBounds.Bottom;

                    if (isFullscreen && (matchProcess || matchClass || matchTitle))
                    {
                        isVideo = true;
                    }
                }

                _lastVideoDetectionWindow = hwnd;
                _lastVideoDetectionTime = now;
                _lastVideoDetectionResult = isVideo;
                return isVideo;
            }
            catch
            {
                InvalidateVideoDetectionCache();
                return false;
            }
        }
    }
}
