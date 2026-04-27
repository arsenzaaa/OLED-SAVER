#nullable disable

namespace OLEDSaver
{
    public class MonitorSettings
    {
        public string DeviceName { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public int TimeoutSeconds { get; set; } = 25;
        public string DisplayName { get; set; } = string.Empty;
    }

    static partial class Program
    {
    }
}
