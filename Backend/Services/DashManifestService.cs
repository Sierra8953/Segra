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
        /// for a specific game into a single continuous timeline using Multi-Period DASH.
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

                // 2. Filter segments to respect the buffer limit (Keep last X seconds)
                // We determine the global time window based on the Video stream (Stream 0) across ALL sessions
                var videoSegments = allSegments.Where(s => s.StreamId == 0).OrderBy(s => s.CreationTime).ToList();

                if (videoSegments.Count == 0)
                {
                    Log.Warning($"No video segments (stream 0) found for game: {gameName}");
                    // Even if no video segments, return empty string which causes 404 is technically correct,
                    // but we should log why.
                    return "";
                }

                // Latest time in video stream
                DateTime maxTime = videoSegments.Max(s => s.CreationTime);
                DateTime minTime = maxTime.AddSeconds(-bufferDurationSeconds);

                // Filter all segments (audio and video) to this window
                var filteredSegments = allSegments
                    .Where(s => s.CreationTime >= minTime)
                    .OrderBy(s => s.CreationTime)
                    .ToList();

                if (filteredSegments.Count == 0)
                {
                    Log.Warning($"No segments found within buffer window ({bufferDurationSeconds}s) for game: {gameName}");
                    return "";
                }

                // 3. Group segments by Session (Folder) to create Periods
                // The order of keys (Folders) should be chronological based on the first segment in each group
                var sessionGroups = filteredSegments
                    .GroupBy(s => s.Folder)
                    .Select(g => new { Folder = g.Key, Segments = g.ToList(), StartTime = g.Min(s => s.CreationTime) })
                    .OrderBy(g => g.StartTime)
                    .ToList();

                // Determine Codec Strings
                string videoCodec = "avc1.640028"; // Default H.264
                // Check Settings for codec hint
                // If the user selected AV1, use a generic AV1 codec string acceptable by most browsers
                // e.g. "av01.0.05M.08" (Main Profile, Level 3.0, Main tier, 8-bit) - safe default for 1080p
                var currentCodec = Settings.Instance.Codec;
                if (currentCodec != null)
                {
                    if (currentCodec.InternalEncoderId.Contains("av1", StringComparison.OrdinalIgnoreCase))
                    {
                        videoCodec = "av01.0.05M.08";
                    }
                    else if (currentCodec.InternalEncoderId.Contains("hevc", StringComparison.OrdinalIgnoreCase))
                    {
                        videoCodec = "hvc1.1.6.L93.B0"; // Generic HEVC
                    }
                }

                // 4. Construct the MPD XML
                // Anchor time: Creation time of the oldest kept video segment in the first period
                DateTime availabilityStartTime = sessionGroups.First().StartTime;
                string availabilityStartTimeStr = availabilityStartTime.ToString("yyyy-MM-ddTHH:mm:ssZ");

                var sb = new StringBuilder();
                sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                sb.Append($"<MPD xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns=\"urn:mpeg:dash:schema:mpd:2011\" xsi:schemaLocation=\"urn:mpeg:dash:schema:mpd:2011 http://standards.iso.org/ittf/PubliclyAvailableStandards/MPEG-DASH_schema_files/DASH-MPD.xsd\" type=\"dynamic\" minimumUpdatePeriod=\"PT2S\" availabilityStartTime=\"{availabilityStartTimeStr}\" minBufferTime=\"PT2S\" timeShiftBufferDepth=\"PT{bufferDurationSeconds * 2}S\" profiles=\"urn:mpeg:dash:profile:isoff-live:2011\">");

                TimeSpan accumulatedDuration = TimeSpan.Zero;

                foreach (var session in sessionGroups)
                {
                    // Organize segments by Stream ID within this session
                    var sessionStreams = session.Segments.GroupBy(s => s.StreamId).ToDictionary(g => g.Key, g => g.OrderBy(s => s.Number).ToList());

                    if (!sessionStreams.ContainsKey(0)) continue; // Skip sessions with no video in window

                    var sessionVideo = sessionStreams[0];
                    double sessionDurationSec = sessionVideo.Count * SegmentDuration;

                    // Period ID must be unique
                    string periodId = session.Folder;

                    sb.Append($"<Period id=\"{periodId}\" start=\"PT{accumulatedDuration.TotalSeconds}S\" duration=\"PT{sessionDurationSec}S\">");

                    // Video Adaptation Set (Stream 0)
                    string initVideoPath = $"{session.Folder}/init-stream0.m4s";

                    sb.Append("<AdaptationSet mimeType=\"video/mp4\" segmentAlignment=\"true\" startWithSAP=\"1\" subsegmentAlignment=\"true\" subsegmentStartsWithSAP=\"1\">");
                    sb.Append($"<Representation id=\"0\" codecs=\"{videoCodec}\" bandwidth=\"4000000\">");
                    sb.Append($"<SegmentList timescale=\"{Timescale}\" duration=\"{SegmentDuration * Timescale}\">");
                    sb.Append($"<Initialization sourceURL=\"{initVideoPath}\" />");
                    foreach (var seg in sessionVideo)
                    {
                        sb.Append($"<SegmentURL media=\"{seg.Path}\" />");
                    }
                    sb.Append("</SegmentList>");
                    sb.Append("</Representation>");
                    sb.Append("</AdaptationSet>");

                    // Audio Adaptation Set (Stream 1)
                    if (sessionStreams.ContainsKey(1))
                    {
                        var sessionAudio = sessionStreams[1];
                        string initAudioPath = $"{session.Folder}/init-stream1.m4s";

                        sb.Append("<AdaptationSet mimeType=\"audio/mp4\" segmentAlignment=\"true\" startWithSAP=\"1\">");
                        sb.Append("<Representation id=\"1\" codecs=\"mp4a.40.2\" bandwidth=\"128000\" audioSamplingRate=\"44100\">");
                        sb.Append("<AudioChannelConfiguration schemeIdUri=\"urn:mpeg:dash:23003:3:audio_channel_configuration:2011\" value=\"2\" />");
                        sb.Append($"<SegmentList timescale=\"{Timescale}\" duration=\"{SegmentDuration * Timescale}\">");
                        sb.Append($"<Initialization sourceURL=\"{initAudioPath}\" />");
                        foreach (var seg in sessionAudio)
                        {
                            sb.Append($"<SegmentURL media=\"{seg.Path}\" />");
                        }
                        sb.Append("</SegmentList>");
                        sb.Append("</Representation>");
                        sb.Append("</AdaptationSet>");
                    }

                    sb.Append("</Period>");

                    accumulatedDuration = accumulatedDuration.Add(TimeSpan.FromSeconds(sessionDurationSec));
                }

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
