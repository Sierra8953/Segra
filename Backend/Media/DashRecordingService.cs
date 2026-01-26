using System.Globalization;
using System.Text.RegularExpressions;
using Serilog;

namespace Segra.Backend.Media
{
    public static class DashRecordingService
    {
        // Segment duration: 2 seconds.
        // Rolling Buffer: Maintain a dynamic window (default approx. 900 segments for 30 min).
        // Stitching: Clips must be created using -c copy (stream copy) from the DASH source for instant processing.

        public const string FormatName = "dash";
        public const int DefaultSegmentDuration = 2;

        public static string GetDashFormatName()
        {
            return FormatName;
        }

        public static string GetDashMuxerSettings(int bufferDurationSeconds = 3600, int startNumber = 1)
        {
            int segmentDuration = DefaultSegmentDuration;
            // Ensure window size covers the requested duration
            int windowSize = Math.Max(10, bufferDurationSeconds / segmentDuration);

            // Flags explained:
            // window_size: Number of segments to keep in the rolling buffer
            // remove_at_exit=0: Keep files when process stops (persistence)
            // use_template=1: Use template based segment naming
            // seg_duration: Duration of each chunk
            // start_number: The number to start the sequence from (resuming)
            return $"window_size={windowSize} remove_at_exit=0 use_template=1 seg_duration={segmentDuration} start_number={startNumber}";
        }

        public static string GetOutputFileName()
        {
            return "session.mpd";
        }

        /// <summary>
        /// Scans the directory for existing segments and returns the next segment number in the sequence.
        /// Assumes default ffmpeg DASH template naming (chunk-streamX-YYYYY.m4s).
        /// </summary>
        public static int GetNextSegmentNumber(string folderPath)
        {
            try
            {
                if (!Directory.Exists(folderPath)) return 1;

                // Look for m4s files. Standard template is usually chunk-stream0-00001.m4s or similar.
                // We'll use a regex to capture the number at the end of the filename before extension.
                var files = Directory.GetFiles(folderPath, "*.m4s");
                if (files.Length == 0) return 1;

                int maxNumber = 0;
                // Regex to match "chunk-stream0-<number>.m4s" or any ".*-<number>.m4s" pattern
                var regex = new Regex(@"-(\d+)\.m4s$");

                foreach (var file in files)
                {
                    var match = regex.Match(file);
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int number))
                    {
                        if (number > maxNumber) maxNumber = number;
                    }
                }

                return maxNumber + 1;
            }
            catch (Exception ex)
            {
                Log.Warning($"Failed to determine next segment number: {ex.Message}. Defaulting to 1.");
                return 1;
            }
        }

        /// <summary>
        /// Deletes the oldest segments if the total count exceeds the buffer limit.
        /// This enforces the buffer limit across sessions.
        /// </summary>
        public static void PruneOldSegments(string folderPath, int bufferDurationSeconds)
        {
            try
            {
                if (!Directory.Exists(folderPath)) return;

                int segmentDuration = DefaultSegmentDuration;
                int maxSegments = Math.Max(10, bufferDurationSeconds / segmentDuration);

                var regex = new Regex(@"-(\d+)\.m4s$");
                var segments = new List<(string Path, int Number)>();

                foreach (var file in Directory.GetFiles(folderPath, "*.m4s"))
                {
                    var match = regex.Match(file);
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int number))
                    {
                        segments.Add((file, number));
                    }
                }

                // If we have more segments than allowed, delete the oldest ones (smallest numbers)
                if (segments.Count > maxSegments)
                {
                    // Sort by number ascending
                    segments.Sort((a, b) => a.Number.CompareTo(b.Number));

                    int countToDelete = segments.Count - maxSegments;
                    Log.Information($"Pruning {countToDelete} old segments from {folderPath} (Limit: {maxSegments})");

                    for (int i = 0; i < countToDelete; i++)
                    {
                        try
                        {
                            File.Delete(segments[i].Path);
                            // Also try to delete init segment if it's specific? No, init is usually shared or separate.
                            // But ffmpeg creates 'init-stream0.m4s'. We usually keep that.
                        }
                        catch (Exception ex)
                        {
                            Log.Warning($"Failed to delete old segment {segments[i].Path}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error during segment pruning: {ex.Message}");
            }
        }
    }
}
