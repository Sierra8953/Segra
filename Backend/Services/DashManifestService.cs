using System.Text.RegularExpressions;
using Serilog;
using Segra.Backend.Media;

namespace Segra.Backend.Services
{
    public static class DashManifestService
    {
        /// <summary>
        /// Reads the existing .mpd file and patches it to include all segments found in the folder.
        /// This ensures the player sees the full history (persistent buffer) instead of just the latest session.
        /// </summary>
        public static async Task<string> GetPatchedManifest(string manifestPath, int bufferDurationSeconds)
        {
            try
            {
                if (!File.Exists(manifestPath))
                {
                    Log.Warning($"Manifest not found at {manifestPath}");
                    return "";
                }

                string xml = await File.ReadAllTextAsync(manifestPath);
                string folderPath = Path.GetDirectoryName(manifestPath) ?? "";

                // 1. Find the actual start number from the files on disk
                int minSegmentNumber = GetMinSegmentNumber(folderPath);

                // 2. Patch startNumber in SegmentTemplate
                // Pattern: startNumber="123"
                xml = Regex.Replace(xml, @"startNumber=""\d+""", $"startNumber=\"{minSegmentNumber}\"");

                // 3. Patch timeShiftBufferDepth (DVR window)
                // If it's too small, the player won't let us seek back to the old segments.
                // We set it to a bit more than the configured buffer to be safe.
                // Format: PT3600S
                int bufferDepth = (int)(bufferDurationSeconds * 1.5); // 50% safety margin
                string newBufferDepth = $"timeShiftBufferDepth=\"PT{bufferDepth}S\"";

                if (xml.Contains("timeShiftBufferDepth"))
                {
                    xml = Regex.Replace(xml, @"timeShiftBufferDepth=""[^""]+""", newBufferDepth);
                }
                else
                {
                    // If missing (sometimes ffmpeg omits it), add it to MPD tag
                    xml = Regex.Replace(xml, @"<MPD ", $"<MPD {newBufferDepth} ");
                }

                // 4. Ensure minimumUpdatePeriod is present and reasonable (e.g., 2 seconds)
                // This tells the player to keep refreshing the manifest for live updates.
                if (!xml.Contains("minimumUpdatePeriod"))
                {
                     xml = Regex.Replace(xml, @"<MPD ", $"<MPD minimumUpdatePeriod=\"PT2S\" ");
                }

                return xml;
            }
            catch (Exception ex)
            {
                Log.Error($"Error patching manifest: {ex.Message}");
                // Return original if patching fails (fallback)
                return File.Exists(manifestPath) ? await File.ReadAllTextAsync(manifestPath) : "";
            }
        }

        private static int GetMinSegmentNumber(string folderPath)
        {
            try
            {
                if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) return 1;

                // Look for m4s files matching standard pattern
                var files = Directory.GetFiles(folderPath, "*.m4s");
                if (files.Length == 0) return 1;

                int minNumber = int.MaxValue;
                // Regex matches chunk-streamX-YYYYY.m4s
                var regex = new Regex(@"-(\d+)\.m4s$");

                foreach (var file in files)
                {
                    // Skip init segments (init-stream0.m4s) which don't have numbers in the same place usually
                    if (Path.GetFileName(file).StartsWith("init-")) continue;

                    var match = regex.Match(file);
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int number))
                    {
                        if (number < minNumber) minNumber = number;
                    }
                }

                return minNumber == int.MaxValue ? 1 : minNumber;
            }
            catch (Exception ex)
            {
                Log.Warning($"Failed to determine min segment number: {ex.Message}");
                return 1;
            }
        }
    }
}
