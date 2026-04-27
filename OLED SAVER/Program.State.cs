using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;
using File = System.IO.File;

#nullable disable

namespace OLEDSaver
{
    static partial class Program
    {
        private const uint RDW_INVALIDATE = 0x0001;
        private const uint RDW_UPDATENOW = 0x0100;
        private const uint RDW_ALLCHILDREN = 0x0080;
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const uint SPIF_SENDCHANGE = 0x02;
        private const uint GA_ROOT = 2;
        private const uint THREAD_SET_INFORMATION = 0x0020;
        private const uint THREAD_QUERY_INFORMATION = 0x0040;
        private const int THREAD_PRIORITY_IDLE = -15;
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_MONITORPOWER = 0xF170;
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        private const int SPI_GETWORKAREA = 0x0030;
        private const int SPI_SETWORKAREA = 0x002F;
        private const int ENUM_CURRENT_SETTINGS = -1;
        private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        private const int ActiveEdgePollingIntervalMs = 250;
        private const int IdleEdgePollingIntervalMs = 250;
        private const int DisabledEdgePollingIntervalMs = 1000;
        private const int DefaultInactivityPollingIntervalMs = 700;
        private const int QuiescentInactivityPollingIntervalMs = 2000;
        private const int TaskbarStatePollingIntervalMs = 200;
        private const int DesktopIconRefreshDebounceMs = 150;
        private const int MaxOverlayRefreshRate = 60;
        private const int OverlaySlowPollIntervalMs = 750;
        private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xFFFF);

        private static Timer _edgeTimer;
        private static Timer _inactivityTimer;
        private static Timer _desktopIconRefreshTimer;
        private static Point _lastCursorPos = Point.Empty;
        private static DateTime _lastActiveTime = DateTime.Now;
        private static readonly TimeSpan InactivityThreshold = TimeSpan.FromSeconds(1);
        private static DateTime _lastWindowsKeyTime = DateTime.MinValue;
        private static readonly TimeSpan _winKeyShowDuration = TimeSpan.FromSeconds(3);
        private static bool _enableInterpolation = true;
        private static Rectangle _previousTargetRect = Rectangle.Empty;
        private static Rectangle _lastWindowBounds = Rectangle.Empty;
        private static Rectangle _lastActiveWindowRect = Rectangle.Empty;
        private static Rectangle _targetActiveWindowRect = Rectangle.Empty;
        private static Rectangle _currentActiveWindowRect = Rectangle.Empty;
        private static Rectangle _lastRenderedOverlayRect = Rectangle.Empty;
        private static bool _drawBlackOverlay = false;
        private static bool _taskbarHidingEnabled = true;
        private static bool _desktopIconsHidingEnabled = true;
        private static bool _screenOffEnabled = true;
        private static bool _overlayFaded = false;
        private static bool _isVideoPlaying = false;
        private static bool _desktopIconsHidden = false;
        private static bool _drawBlackOverlayEnabled = false;
        private static bool _taskbarHidden = false;
        private static bool _screenOff = false;
        private static bool _cursorHiddenByOverlays = false;
        private static int _displayOffTimeoutSeconds = 60;
        private static int _desktopIconsTimeoutSeconds = 3;
        private static int _drawBlackOverlayEnabledTimeoutSeconds = 2;
        private static int _taskbarTimeoutSeconds = 1;
        public static int _activityThreshold = 130;
        private static IntPtr _windowThatTriggeredOverlay = IntPtr.Zero;
        private static bool _overlayRoundedCorners = true;
        private static double _overlayOpacity = 0.93;
        private static double _overlayFadedOpacity = 0.6;
        private static RECT _originalWorkArea;
        private static bool _workAreaStored = false;
        private static NotifyIcon _trayIcon;
        private static CancellationTokenSource _overlayUpdateCts;
        private static ContextMenuStrip _contextMenu;
        private static Dictionary<Screen, Form> _overlays = new();
        private static Dictionary<string, MonitorSettings> _monitorSettings = new();
        private static IntPtr _hookHandle = IntPtr.Zero;
        private static WinEventDelegate _winEventProc = WinEventProc;
        private static List<string> _excludedWindowTitles = new();
        private static DateTime _lastTaskbarInteractionTime = DateTime.MinValue;
        private static IntPtr _lastVideoDetectionWindow = IntPtr.Zero;
        private static DateTime _lastVideoDetectionTime = DateTime.MinValue;
        private static bool _lastVideoDetectionResult = false;
        private static readonly string StartupFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        private static readonly string ShortcutPath = Path.Combine(StartupFolderPath, "OLEDSaver.lnk");
        private static readonly TimeSpan VideoDetectionCacheDuration = TimeSpan.FromMilliseconds(750);
        private const string TrayIconResourceName = "OLEDSaver.Resources.OledSaverIcon";
        private static readonly string[] DesktopWindowClasses =
        {
            "Progman",
            "WorkerW",
            "SHELLDLL_DefView",
            "SysListView32"
        };

        private static readonly string[] _videoApps =
        {
            "chrome", "firefox", "edge", "opera", "brave",
            "vivaldi", "yandex", "browser", "thorium", "mercury",
            "vlc", "mpc", "potplayer", "kmplayer", "gom", "mpv",
            "wmplayer", "quicktime", "realplayer", "winamp", "foobar",
            "aimp", "smplayer", "bsplayer", "cyberlink", "powerdvd",
            "media player", "videowindowclass", "vlcvideohwnd",
            "youtube", "twitch", "netflix", "hulu", "prime video",
            "disney", "hbo", "paramount", "peacock", "crunchyroll",
            "funimation", "vimeo", "dailymotion", "tiktok", "itunes",
            "premiere", "vegas", "davinci", "camtasia", "filmora",
            "after effects", "avid", "final cut",
            "zoom", "teams", "whatsapp", "viber", "hangouts", "meet", "webex", "gotomeeting",
            "kodi", "plex", "emby", "jellyfin", "media center",
            "xbmc", "mediaportal"
        };

        private static readonly string[] _videoTitleIndicators =
        {
            "playing", "paused", "buffering", "live", "stream",
            "video", "movie", "film", "episode", "season",
            "watch", "player", "▶", "⏸", "⏯", "●", "🔴",
            "воспроизведение", "пауза", "прямой эфир", "стрим"
        };
    }
}
