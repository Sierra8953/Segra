using System.Text;
using System.Text.RegularExpressions;
using Serilog;
using Segra.Backend.Media;
using Segra.Backend.Shared;
using Segra.Backend.Core.Models;

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

                        // Parse stream ID and segment number from chunk-streamX-YYYYY.m4s
                        // Regex matches: chunk-stream(\d+)-(\d+).m4s
                        var match = Regex.Match(fileName, @"chunk-stream(\d+)-(\d+)\.m4s$");
                        if (match.Success &&
                            int.TryParse(match.Groups[1].Value, out int streamId) &&
                            int.TryParse(match.Groups[2].Value, out int number))
                        {
                            var creationTime = File.GetCreationTimeUtc(file);

                            // Store relative path for the client to request: {TimestampFolder}/{FileName}
                            string relativePath = $"{folderName}/{fileName}";

                            allSegments.Add(new SegmentInfo
                            {
                                Path = relativePath,
                                Number = number,
                                CreationTime = creationTime,
                                Folder = folderName,
                                StreamId = streamId
                            });
                        }
                    }
                }

                // 2. Group by Stream ID
                // We typically expect Stream 0 to be Video and Stream 1 to be Audio.
                var streams = allSegments.GroupBy(s => s.StreamId).ToDictionary(g => g.Key, g => g.ToList());

                if (!streams.ContainsKey(0))
                {
                    // If no stream 0, we can't play video.
                    Log.Warning($"No video segments (stream 0) found for game: {gameName}");
                    return "";
                }

                // 3. Filter segments to respect the buffer limit (Keep last X seconds)
                // We determine the time window based on the Video stream (Stream 0)
                var videoSegments = streams[0];
                videoSegments.Sort((a, b) => a.CreationTime.CompareTo(b.CreationTime));

                // Latest time in video stream
                DateTime maxTime = videoSegments.Max(s => s.CreationTime);
                DateTime minTime = maxTime.AddSeconds(-bufferDurationSeconds);

                // Filter all streams to this window
                var finalStreams = new Dictionary<int, List<SegmentInfo>>();

                // We only care about Stream 0 (Video) and Stream 1 (Audio) for preview to ensure stability
                int[] allowedStreams = { 0, 1 };

                foreach (var id in allowedStreams)
                {
                    if (streams.ContainsKey(id))
                    {
                        var filtered = streams[id]
                            .Where(s => s.CreationTime >= minTime)
                            .OrderBy(s => s.CreationTime) // Ensure sorted by time
                            .ToList();

                        if (filtered.Count > 0)
                        {
                            finalStreams[id] = filtered;
                        }
                    }
                }

                if (!finalStreams.ContainsKey(0) || finalStreams[0].Count == 0) return "";

                var finalVideoSegments = finalStreams[0];

                // 4. Construct the MPD XML
                // Anchor time: Creation time of the oldest kept video segment
                DateTime availabilityStartTime = finalVideoSegments[0].CreationTime;

                // Format for MPD: YYYY-MM-DDTHH:mm:ssZ
                string availabilityStartTimeStr = availabilityStartTime.ToString("yyyy-MM-ddTHH:mm:ssZ");

                // Get the initialization segment paths (assume newest session's init is valid)
                string newestVideoFolder = finalVideoSegments.Last().Folder;
                string initVideoPath = $"{newestVideoFolder}/init-stream0.m4s";

                var sb = new StringBuilder();
                sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                sb.Append($"<MPD xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns=\"urn:mpeg:dash:schema:mpd:2011\" xsi:schemaLocation=\"urn:mpeg:dash:schema:mpd:2011 http://standards.iso.org/ittf/PubliclyAvailableStandards/MPEG-DASH_schema_files/DASH-MPD.xsd\" type=\"dynamic\" minimumUpdatePeriod=\"PT2S\" availabilityStartTime=\"{availabilityStartTimeStr}\" minBufferTime=\"PT2S\" timeShiftBufferDepth=\"PT{bufferDurationSeconds * 2}S\" profiles=\"urn:mpeg:dash:profile:isoff-live:2011\">");

                // Duration is based on video segments count
                sb.Append($"<Period start=\"PT0S\" id=\"0\" duration=\"PT{finalVideoSegments.Count * SegmentDuration}S\">");

                // Video Adaptation Set
                sb.Append("<AdaptationSet mimeType=\"video/mp4\" segmentAlignment=\"true\" startWithSAP=\"1\" subsegmentAlignment=\"true\" subsegmentStartsWithSAP=\"1\">");
                sb.Append("<Representation id=\"0\" codecs=\"avc1.640028\" bandwidth=\"4000000\">");
                sb.Append($"<SegmentList timescale=\"{Timescale}\" duration=\"{SegmentDuration * Timescale}\">");
                sb.Append($"<Initialization sourceURL=\"{initVideoPath}\" />");

                foreach (var seg in finalVideoSegments)
                {
                    sb.Append($"<SegmentURL media=\"{seg.Path}\" />");
                }

                sb.Append("</SegmentList>");
                sb.Append("</Representation>");
                sb.Append("</AdaptationSet>");

                // Audio Adaptation Set (Stream 1)
                if (finalStreams.ContainsKey(1))
                {
                    var audioSegments = finalStreams[1];
                    string newestAudioFolder = audioSegments.Last().Folder;
                    string initAudioPath = $"{newestAudioFolder}/init-stream1.m4s";

                    sb.Append("<AdaptationSet mimeType=\"audio/mp4\" segmentAlignment=\"true\" startWithSAP=\"1\">");
                    // Assuming AAC audio, typical bandwidth/codec
                    sb.Append("<Representation id=\"1\" codecs=\"mp4a.40.2\" bandwidth=\"128000\" audioSamplingRate=\"44100\">");
                    sb.Append("<AudioChannelConfiguration schemeIdUri=\"urn:mpeg:dash:23003:3:audio_channel_configuration:2011\" value=\"2\" />");
                    sb.Append($"<SegmentList timescale=\"{Timescale}\" duration=\"{SegmentDuration * Timescale}\">");
                    sb.Append($"<Initialization sourceURL=\"{initAudioPath}\" />");

                    foreach (var seg in audioSegments)
                    {
                        sb.Append($"<SegmentURL media=\"{seg.Path}\" />");
                    }

                    sb.Append("</SegmentList>");
                    sb.Append("</Representation>");
                    sb.Append("</AdaptationSet>");
                }

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
            public int StreamId { get; set; }
            public DateTime CreationTime { get; set; }
            public required string Folder { get; set; }
        }
    }
}
