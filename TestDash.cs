using System;
using System.IO;
using System.Threading.Tasks;
using Segra.Backend.Media;
using Segra.Backend.Services;
using Segra.Backend.Shared;
using Segra.Backend.Windows.Storage;

// Mock dependencies if needed, or simple standalone test
public class TestDashLogic
{
    public static async Task Run()
    {
        Console.WriteLine("Starting DASH Logic Test...");

        string gameName = "TestGame";
        string sanitizedGame = "TestGame";
        string contentFolder = Path.Combine(Directory.GetCurrentDirectory(), "TestContent");
        string sessionsFolder = Path.Combine(contentFolder, "Sessions");
        string gameFolder = Path.Combine(sessionsFolder, sanitizedGame);
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string sessionDir = Path.Combine(gameFolder, timestamp);
        string liveManifestPath = Path.Combine(sessionDir, $"{timestamp}.mpd");
        string masterManifestPath = Path.Combine(gameFolder, $"{sanitizedGame}.mpd");

        Directory.CreateDirectory(sessionDir);

        Console.WriteLine($"Live Path: {liveManifestPath}");
        Console.WriteLine($"Master Path: {masterManifestPath}");

        // Start Monitoring
        Console.WriteLine("Starting Monitor...");
        await DashManifestService.StartMonitoring(gameName, liveManifestPath);

        // Verify Master does NOT exist yet (removed CreateInitial)
        if (File.Exists(masterManifestPath))
        {
            Console.WriteLine("FAILURE: Master manifest created too early (should wait for live content).");
        }
        else
        {
            Console.WriteLine("SUCCESS: Master manifest not created yet.");
        }

        // Simulate OBS writing live manifest after 2 seconds
        await Task.Delay(2000);
        Console.WriteLine("Simulating OBS write...");
        File.WriteAllText(liveManifestPath, @"<?xml version=""1.0""?>
<MPD xmlns=""urn:mpeg:dash:schema:mpd:2011"" profiles=""urn:mpeg:dash:profile:isoff-live:2011"" type=""dynamic"" minBufferTime=""PT1.5S"">
  <Period>
    <AdaptationSet mimeType=""video/mp4"">
      <SegmentTemplate media=""chunk.m4s"" initialization=""init.mp4"" />
    </AdaptationSet>
  </Period>
</MPD>");

        // Monitor should pick it up and create Master
        Console.WriteLine("Waiting for Monitor to sync...");
        await Task.Delay(2000);

        if (File.Exists(masterManifestPath))
        {
            Console.WriteLine("SUCCESS: Master manifest created.");
            string content = File.ReadAllText(masterManifestPath);
            if (content.Contains("<Period") && content.Contains("AdaptationSet"))
            {
                Console.WriteLine("SUCCESS: Master manifest contains period data.");
            }
            else
            {
                Console.WriteLine("FAILURE: Master manifest is empty or invalid.");
                Console.WriteLine(content);
            }
        }
        else
        {
            Console.WriteLine("FAILURE: Master manifest NOT created.");
        }

        DashManifestService.StopMonitoring();

        // Cleanup
        // Directory.Delete(contentFolder, true);
    }
}
