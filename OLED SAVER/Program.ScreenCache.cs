using System;
using System.Windows.Forms;
using Microsoft.Win32;

#nullable disable

namespace OLEDSaver
{
    static partial class Program
    {
        private static readonly CachedSnapshot<Screen> _screenSnapshot =
            new CachedSnapshot<Screen>(() => Screen.AllScreens);

        private static void StartScreenSnapshotInvalidation()
        {
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }

        private static void StopScreenSnapshotInvalidation()
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        }

        private static void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            _screenSnapshot.Invalidate();
        }

        private static Screen[] GetScreens()
        {
            return _screenSnapshot.Get();
        }

        private static Screen GetPrimaryScreen()
        {
            return Screen.PrimaryScreen ?? GetScreens()[0];
        }
    }
}
