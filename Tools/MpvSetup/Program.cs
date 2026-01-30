using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;

class Program
{
    private const string MpvUrl = "https://github.com/shinchiro/mpv-winbuild-cmake/releases/download/20260122/mpv-x86_64-v3-20260122-git-6e54aa3.7z";

    static async Task Main(string[] args)
    {
        // Calculate paths relative to the project root
        // When running via 'dotnet run --project ...', CWD is the project folder (Tools/MpvSetup) unless specified.
        // However, we want to be robust.

        // Find repo root:
        var currentDir = Directory.GetCurrentDirectory();

        string targetDir;
        if (File.Exists(Path.Combine(currentDir, "Segra.sln")))
        {
            targetDir = Path.Combine(currentDir, "Backend", "mpv");
        }
        else
        {
             // Assume we are in Tools/MpvSetup or deeper
             var dir = new DirectoryInfo(currentDir);
             while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Segra.sln")))
             {
                 dir = dir.Parent;
             }
             if (dir == null)
             {
                 Console.WriteLine("Could not find repository root.");
                 Environment.Exit(1);
                 return;
             }
             targetDir = Path.Combine(dir.FullName, "Backend", "mpv");
        }

        string tempDir = Path.Combine(Path.GetDirectoryName(targetDir)!, "mpv_temp");
        string archivePath = Path.Combine(Path.GetTempPath(), "mpv_build.7z");

        // Check if mpv exists
        if (File.Exists(Path.Combine(targetDir, "mpv.exe")))
        {
            Console.WriteLine("MPV already present in Backend/mpv.");
            return;
        }

        Console.WriteLine($"Downloading MPV from {MpvUrl}...");

        using (var client = new HttpClient())
        using (var response = await client.GetAsync(MpvUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var fileStream = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await stream.CopyToAsync(fileStream);
            }
        }
        Console.WriteLine("Download complete.");

        Console.WriteLine("Extracting...");
        if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);
        Directory.CreateDirectory(targetDir);

        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        Directory.CreateDirectory(tempDir);

        try
        {
            using (var archive = ArchiveFactory.Open(archivePath))
            {
                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                {
                    entry.WriteToDirectory(tempDir, new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
                }
            }

            // Flatten
            var mpvExe = Directory.GetFiles(tempDir, "mpv.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (mpvExe != null)
            {
                string parentDir = Path.GetDirectoryName(mpvExe)!;
                foreach (var file in Directory.GetFiles(parentDir))
                {
                    string dest = Path.Combine(targetDir, Path.GetFileName(file));
                    File.Move(file, dest);
                }
                 foreach (var dir in Directory.GetDirectories(parentDir))
                {
                    string dest = Path.Combine(targetDir, Path.GetFileName(dir));
                    Directory.Move(dir, dest);
                }
            }
            else
            {
                 Console.WriteLine("Error: mpv.exe not found in archive.");
            }
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            if (File.Exists(archivePath)) File.Delete(archivePath);
        }

        Console.WriteLine("MPV setup complete.");
    }
}
