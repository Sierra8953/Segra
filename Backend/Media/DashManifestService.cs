using System.Xml.Linq;
using System.Globalization;
using Segra.Backend.Shared;
using Serilog;
using Segra.Backend.Services;
using Segra.Backend.Windows.Storage;

namespace Segra.Backend.Media
{
    public static class DashManifestService
    {
        private static CancellationTokenSource? _monitoringCts;
        private static string? _currentMasterManifestPath;
        private static string? _currentLiveManifestPath;
        private static string? _currentGame;
        private static string? _currentRelativeBaseUrl;

        // XML Namespaces for DASH
        private static readonly XNamespace NS = "urn:mpeg:dash:schema:mpd:2011";

        public static async Task StartMonitoring(string game, string liveManifestPath)
        {
            StopMonitoring(); // Ensure previous is stopped

            _monitoringCts = new CancellationTokenSource();
            _currentGame = game;
            _currentLiveManifestPath = liveManifestPath;

            // Master manifest lives in the Game folder: Sessions/{Game}/{Game}.mpd
            // liveManifestPath is: Sessions/{Game}/{Timestamp}/{Timestamp}.mpd
            string sessionDir = Path.GetDirectoryName(Path.GetDirectoryName(liveManifestPath))!; // Sessions/{Game}
            _currentMasterManifestPath = Path.Combine(sessionDir, $"{StorageService.SanitizeGameNameForFolder(game)}.mpd");

            // Calculate relative path for BaseURL (e.g., "./2026-01-01_12-00-00/")
            // liveManifestPath dir: .../Timestamp/
            string timestampDirName = new DirectoryInfo(Path.GetDirectoryName(liveManifestPath)!).Name;
            _currentRelativeBaseUrl = $"./{timestampDirName}/";

            Log.Information($"[DashManifestService] Starting monitor. Master: {_currentMasterManifestPath}, Live: {_currentLiveManifestPath}");

            // Ensure master manifest exists immediately so metadata can be created
            if (!File.Exists(_currentMasterManifestPath))
            {
                await CreateInitialMasterManifest();
            }

            // Run background task
            _ = Task.Run(() => MonitorLoop(_monitoringCts.Token));
        }

        private static async Task CreateInitialMasterManifest()
        {
            try
            {
                var masterDoc = new XDocument(
                        new XDeclaration("1.0", "utf-8", null),
                        new XElement(NS + "MPD",
                            new XAttribute("xmlns", NS.NamespaceName),
                            new XAttribute("minBufferTime", "PT1.5S"),
                            new XAttribute("type", "dynamic"),
                            new XAttribute("profiles", "urn:mpeg:dash:profile:isoff-live:2011"),
                            new XAttribute("publishTime", DateTime.UtcNow.ToString("O")),
                            new XAttribute("availabilityStartTime", DateTime.UtcNow.ToString("O")),
                            new XAttribute("minimumUpdatePeriod", "PT1S")
                        )
                    );

                using (var fs = new FileStream(_currentMasterManifestPath!, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await masterDoc.SaveAsync(fs, SaveOptions.None, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[DashManifestService] Failed to create initial master manifest: {ex.Message}");
            }
        }

        public static void StopMonitoring()
        {
            if (_monitoringCts != null)
            {
                _monitoringCts.Cancel();
                _monitoringCts.Dispose();
                _monitoringCts = null;
                Log.Information("[DashManifestService] Monitoring stopped.");
            }
        }

        private static async Task MonitorLoop(CancellationToken token)
        {
            try
            {
                // Wait for the live manifest to be created by OBS
                while (!File.Exists(_currentLiveManifestPath) && !token.IsCancellationRequested)
                {
                    await Task.Delay(500, token);
                }

                if (token.IsCancellationRequested) return;

                // Initial Merge/Create
                await UpdateMasterManifest();

                // Polling loop
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(1000, token); // Poll every 1s
                    await UpdateMasterManifest();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Log.Error($"[DashManifestService] Error in monitor loop: {ex.Message}");
            }
        }

        private static async Task UpdateMasterManifest()
        {
            try
            {
                if (!File.Exists(_currentLiveManifestPath)) return;

                // 1. Read Live Manifest
                // We use FileShare.ReadWrite because OBS is writing to it
                XDocument liveDoc;
                using (var fs = new FileStream(_currentLiveManifestPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    liveDoc = await XDocument.LoadAsync(fs, LoadOptions.None, CancellationToken.None);
                }

                // 2. Read or Create Master Manifest
                XDocument masterDoc;
                bool isNewMaster = false;
                if (File.Exists(_currentMasterManifestPath))
                {
                    using (var fs = new FileStream(_currentMasterManifestPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        masterDoc = await XDocument.LoadAsync(fs, LoadOptions.None, CancellationToken.None);
                    }
                }
                else
                {
                    // Create basic structure
                    masterDoc = new XDocument(
                        new XDeclaration("1.0", "utf-8", null),
                        new XElement(NS + "MPD",
                            new XAttribute("xmlns", NS.NamespaceName),
                            new XAttribute("minBufferTime", "PT1.5S"),
                            new XAttribute("type", "static"), // "dynamic" is harder for multi-period stitching without valid availabilityStartTime
                            new XAttribute("profiles", "urn:mpeg:dash:profile:isoff-live:2011")
                        )
                    );
                    isNewMaster = true;
                }

                // 3. Merge Logic
                // We treat the current Live Session as a NEW Period in the Master Manifest.
                // Or if the Period already exists, we update it.

                // Identification of the Period: Use BaseURL as ID? Or strict index.
                // We look for a Period with BaseURL == _currentRelativeBaseUrl

                var mpd = masterDoc.Root!;

                // Ensure type is static or dynamic?
                // If we want "One continuous recording" that behaves like VOD (seekable), "static" is best.
                // But while recording, "dynamic" allows player to poll.
                // Let's stick to "dynamic" if live, but we need to manage availabilityStartTime.
                // EASIER: Just use "static" and update duration. dash.js reloads manifest if needed?
                // Actually, for "Live Preview", we usually want "dynamic".
                // Let's copy root attributes from Live if New.
                if (isNewMaster)
                {
                    var liveRoot = liveDoc.Root!;
                    // Copy key attributes
                    // Set type to dynamic for live behavior
                    mpd.SetAttributeValue("type", "dynamic");
                    mpd.SetAttributeValue("minBufferTime", liveRoot.Attribute("minBufferTime")?.Value);
                    mpd.SetAttributeValue("publishTime", DateTime.UtcNow.ToString("O"));
                    mpd.SetAttributeValue("availabilityStartTime", liveRoot.Attribute("availabilityStartTime")?.Value ?? DateTime.UtcNow.ToString("O"));
                    // MinimumUpdatePeriod needed for polling
                    mpd.SetAttributeValue("minimumUpdatePeriod", "PT1S");
                }

                // Find or Create Period
                var period = mpd.Elements(NS + "Period").FirstOrDefault(p => p.Element(NS + "BaseURL")?.Value == _currentRelativeBaseUrl);

                var livePeriod = liveDoc.Root!.Element(NS + "Period");
                if (livePeriod == null) return; // Should not happen

                if (period == null)
                {
                    // Create new Period
                    period = new XElement(NS + "Period");

                    // Add BaseURL pointing to subfolder
                    period.Add(new XElement(NS + "BaseURL", _currentRelativeBaseUrl));

                    // Add to Master
                    mpd.Add(period);
                }

                // Update Period attributes (duration, id) from Live
                if (livePeriod.Attribute("id") != null) period.SetAttributeValue("id", livePeriod.Attribute("id")!.Value + "_" + _currentRelativeBaseUrl);
                if (livePeriod.Attribute("start") != null)
                {
                    // For multi-period, 'start' is relative to availabilityStartTime.
                    // If we just append, we might not need 'start' if we use duration?
                    // Safe bet: Copy it if it's the first period, or calculate offset?
                    // Actually, if we just strip 'start', periods play sequentially.
                    // period.SetAttributeValue("start", livePeriod.Attribute("start").Value);
                }

                // 4. Update AdaptationSets (Video/Audio)
                // We replace the content of the Period with the Live Period content (AdaptationSets),
                // BUT we must preserve the BaseURL we added.

                // Clear existing AdaptationSets in this Period (refresh them from Live)
                period.Elements(NS + "AdaptationSet").Remove();

                // Import AdaptationSets from Live
                foreach (var adaptSet in livePeriod.Elements(NS + "AdaptationSet"))
                {
                    // We need to clone it
                    period.Add(adaptSet);
                }

                // Ensure BaseURL is first child (cosmetic, but good practice)
                var baseUrlNode = period.Element(NS + "BaseURL");
                if (baseUrlNode != null)
                {
                    baseUrlNode.Remove();
                    period.AddFirst(baseUrlNode);
                }

                // 5. Save Master Manifest
                // Atomic write
                string tempPath = _currentMasterManifestPath + ".tmp";
                using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await masterDoc.SaveAsync(fs, SaveOptions.None, CancellationToken.None);
                }
                File.Move(tempPath, _currentMasterManifestPath, true);
            }
            catch (Exception ex)
            {
                Log.Warning($"[DashManifestService] Failed to update master manifest: {ex.Message}");
            }
        }
    }
}
