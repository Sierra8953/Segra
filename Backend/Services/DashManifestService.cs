using System.Text;
using System.Text.RegularExpressions;
using Serilog;
using Segra.Backend.Media;
using Segra.Backend.Shared;

namespace Segra.Backend.Services
{
    public static class DashManifestService
    {
        // Constants for DASH generation
        private const int SegmentDuration = 2; // Matches DashRecordingService.DefaultSegmentDuration
        private const int Timescale = 1000;

        /// <summary>
        /// Generates a "Virtual Manifest" that stitches together all DASH segments from all session folders
        /// for a specific game into a single continuous timeline.
        /// </summary>
        public static async Task<string> GetVirtualManifest(string gameName, int bufferDurationSeconds)
        {
            try
            {
                string sessionsRoot = Path.Combine(Settings.Instance.ContentFolder, FolderNames.Sessions, gameName);
                if (!Directory.Exists(sessionsRoot))
                {
                    Log.Warning($"Session folder not found for game: {gameName}");
                    return "";
                }

                // 1. Scan for all .m4s segments across all subdirectories
                var allSegments = new List<SegmentInfo>();
                var sessionFolders = Directory.GetDirectories(sessionsRoot);

                foreach (var folder in sessionFolders)
                {
                    // Folder name is timestamp: yyyy-MM-dd_HH-mm-ss
                    var folderName = Path.GetFileName(folder);

                    var files = Directory.GetFiles(folder, "*.m4s");
                    foreach (var file in files)
                    {
                        var fileName = Path.GetFileName(file);
                        // Filter out init segments, we handle them separately
                        if (fileName.StartsWith("init-")) continue;

                        // Parse segment number from chunk-stream0-XXXXX.m4s
                        // Match any standard DASH segment pattern
                        var match = Regex.Match(fileName, @"-(\d+)\.m4s$");
                        if (match.Success && int.TryParse(match.Groups[1].Value, out int number))
                        {
                            // We use the file creation time as the absolute source of truth for ordering
                            // This handles crashes/restarts better than relying purely on folder names
                            var creationTime = File.GetCreationTimeUtc(file);

                            // Store relative path for the client to request: {TimestampFolder}/{FileName}
                            string relativePath = $"{folderName}/{fileName}";

                            allSegments.Add(new SegmentInfo
                            {
                                Path = relativePath,
                                Number = number,
                                CreationTime = creationTime,
                                Folder = folderName
                            });
                        }
                    }
                }

                // 2. Sort all segments by creation time
                allSegments.Sort((a, b) => a.CreationTime.CompareTo(b.CreationTime));

                if (allSegments.Count == 0) return "";

                // 3. Filter segments to respect the buffer limit (Keep last X seconds)
                // Since each segment is ~2s, we keep (bufferDuration / 2) segments
                int maxSegments = Math.Max(10, bufferDurationSeconds / SegmentDuration);
                if (allSegments.Count > maxSegments)
                {
                    // Keep the newest ones
                    allSegments = allSegments.GetRange(allSegments.Count - maxSegments, maxSegments);
                }

                if (allSegments.Count == 0) return "";

                // 4. Construct the MPD XML
                // Anchor time: Creation time of the oldest kept segment
                DateTime availabilityStartTime = allSegments[0].CreationTime;

                // Format for MPD: YYYY-MM-DDTHH:mm:ssZ
                string availabilityStartTimeStr = availabilityStartTime.ToString("yyyy-MM-ddTHH:mm:ssZ");

                // Get the initialization segment path (assume the newest session's init segment is valid for all)
                // Ideally, they are identical if encoding settings haven't changed.
                // We'll use the folder of the *newest* segment to find the init file.
                string newestFolder = allSegments.Last().Folder;
                string initSegmentPath = $"{newestFolder}/init-stream0.m4s";

                var sb = new StringBuilder();
                sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                sb.Append($"<MPD xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns=\"urn:mpeg:dash:schema:mpd:2011\" xsi:schemaLocation=\"urn:mpeg:dash:schema:mpd:2011 http://standards.iso.org/ittf/PubliclyAvailableStandards/MPEG-DASH_schema_files/DASH-MPD.xsd\" type=\"dynamic\" minimumUpdatePeriod=\"PT2S\" availabilityStartTime=\"{availabilityStartTimeStr}\" minBufferTime=\"PT2S\" timeShiftBufferDepth=\"PT{bufferDurationSeconds * 2}S\" profiles=\"urn:mpeg:dash:profile:isoff-live:2011\">");
                sb.Append($"<Period start=\"PT0S\" id=\"0\" duration=\"PT{allSegments.Count * SegmentDuration}S\">");
                sb.Append("<AdaptationSet mimeType=\"video/mp4\" segmentAlignment=\"true\" startWithSAP=\"1\" subsegmentAlignment=\"true\" subsegmentStartsWithSAP=\"1\">");
                sb.Append("<Representation id=\"0\" codecs=\"avc1.640028\" bandwidth=\"4000000\">"); // Codec/bandwidth are placeholders, player usually adapts or ignores if init segment overrides

                // We use SegmentList instead of SegmentTemplate because our files are in different folders (timestamps)
                // and don't follow a single continuous numbering scheme (they reset to 1 in each folder).
                sb.Append($"<SegmentList timescale=\"{Timescale}\" duration=\"{SegmentDuration * Timescale}\">");
                sb.Append($"<Initialization sourceURL=\"{initSegmentPath}\" />");

                foreach (var seg in allSegments)
                {
                    sb.Append($"<SegmentURL media=\"{seg.Path}\" />");
                }

                sb.Append("</SegmentList>");
                sb.Append("</Representation>");
                sb.Append("</AdaptationSet>");
                sb.Append("</Period>");
                sb.Append("</MPD>");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                Log.Error($"Error generating virtual manifest: {ex.Message}");
                return "";
            }
        }

        private class SegmentInfo
        {
            public required string Path { get; set; }
            public int Number { get; set; }
            public DateTime CreationTime { get; set; }
            public required string Folder { get; set; }
        }
    }
}
