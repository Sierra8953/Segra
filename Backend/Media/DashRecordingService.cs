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

        public static string GetDashMuxerSettings(int bufferDurationSeconds = 1800)
        {
            int segmentDuration = DefaultSegmentDuration;
            int windowSize = Math.Max(10, bufferDurationSeconds / segmentDuration);

            // Removed experimental flags (streaming, ldash, write_prft) to prevent heap corruption/crashes in libobs/ffmpeg
            // Stick to core constraints: window_size, remove_at_exit, use_template, seg_duration
            return $"window_size={windowSize} remove_at_exit=1 use_template=1 seg_duration={segmentDuration}";
        }

        public static string GetOutputFileName()
        {
            return "session.mpd";
        }
    }
}
