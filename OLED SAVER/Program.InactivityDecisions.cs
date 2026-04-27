#nullable disable

namespace OLEDSaver
{
    internal static class InactivityDecisions
    {
        public static bool ShouldTurnOffDisplay(
            bool screenOffEnabled,
            bool screenOff,
            double idleSeconds,
            int timeoutSeconds,
            bool isVideoPlaying)
        {
            return screenOffEnabled &&
                   !screenOff &&
                   idleSeconds >= timeoutSeconds &&
                   !isVideoPlaying;
        }
    }
}
