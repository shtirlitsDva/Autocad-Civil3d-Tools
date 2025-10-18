using IntersectUtilities.UtilsCommon.Enums;

namespace NTRExport.Ntr
{
    internal static class TwinLayout
    {
        // Stub: returns vertical separation between lower (supply) and upper (return) pipes in meters
        public static double GetVerticalSeparationMeters(int dn)
        {
            // TODO: replace with PipeScheduleV2-based lookup when available
            return 0.0;
        }

        // Stub: bottom-to-bottom separation (meters) depending on system and series
        public static double GetBottomToBottomSeparationMeters(PipeSystemEnum system, int dn, PipeSeriesEnum series)
        {
            // TODO: replace with PipeScheduleV2-based lookup when available
            return 0.0;
        }
    }
}


