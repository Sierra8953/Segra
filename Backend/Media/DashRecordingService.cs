using System.Globalization;

namespace Segra.Backend.Media
{
    public static class DashRecordingService
    {
        // Segment duration: 2 seconds.
        // Rolling Buffer: Maintain a dynamic window (default approx. 900 segments for 30 min).
        // Stitching: Clips must be created using -c copy (stream copy) from the DASH source for instant processing.
        // flags -window_size X, -remove_at_exit 1, and -use_template 1.

        public const string FormatName = "dash";
        public const int DefaultSegmentDuration = 2;

        public static string GetDashFormatName()
        {
            return FormatName;
        }

        public static string GetDashMuxerSettings(int bufferDurationSeconds = 3600)
        {
            int segmentDuration = DefaultSegmentDuration;
            // Ensure window size covers the requested duration
            int windowSize = Math.Max(10, bufferDurationSeconds / segmentDuration);

            // Flags explained:
            // window_size: Number of segments to keep in the rolling buffer
            // remove_at_exit=1: Delete files when the process stops (handled by OBS shutdown usually, but we also do manual cleanup)
            // use_template=1: Use template based segment naming (essential for predictable segment names)
            // seg_duration: Duration of each chunk
            return $"window_size={windowSize} remove_at_exit=1 use_template=1 seg_duration={segmentDuration}";
        }

        public static string GetOutputFileName()
        {
            return "session.mpd";
        }
    }
}
