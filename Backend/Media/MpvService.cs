using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Segra.Backend.App;
using Serilog;

namespace Segra.Backend.Media
{
    public static class MpvService
    {
        private static Process? _mpvProcess;
        private static bool _isRunning;
        private const string PipeName = "segra_mpv_socket";
        private static readonly string PipePath = $@"\\.\pipe\{PipeName}";
        private static NamedPipeClientStream? _pipeClient;
        private static StreamReader? _pipeReader;
        private static StreamWriter? _pipeWriter;
        private static CancellationTokenSource? _cts;

        // Path to MPV - assume it's in the app directory or downloaded
        private static string MpvPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mpv", "mpv.exe");

        public static bool IsRunning => _isRunning && _mpvProcess != null && !_mpvProcess.HasExited;

        public static async Task StartMpv()
        {
            if (IsRunning) return;

            // Ensure MPV exists (placeholder for download logic)
            if (!File.Exists(MpvPath))
            {
                Log.Error($"MPV executable not found at {MpvPath}");
                // In a real scenario, trigger download here
                return;
            }

            try
            {
                _mpvProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = MpvPath,
                        Arguments = $"--idle=yes --input-ipc-server={PipePath} --keep-open=yes --force-window=yes --title=\"StreamDVR Player\"",
                        UseShellExecute = false,
                        CreateNoWindow = false // Let it create its own window
                    }
                };

                _mpvProcess.Start();
                _isRunning = true;
                Log.Information("MPV process started.");

                // Start IPC connection loop
                _cts = new CancellationTokenSource();
                _ = Task.Run(() => ConnectAndListenIpc(_cts.Token));
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to start MPV: {ex.Message}");
            }
        }

        public static void StopMpv()
        {
            try
            {
                _cts?.Cancel();
                if (_mpvProcess != null && !_mpvProcess.HasExited)
                {
                    _mpvProcess.Kill();
                }
                _isRunning = false;
                _pipeClient?.Dispose();
                _pipeClient = null;
                Log.Information("MPV process stopped.");
            }
            catch (Exception ex)
            {
                Log.Error($"Error stopping MPV: {ex.Message}");
            }
        }

        public static async Task LoadFile(string url)
        {
            await SendCommand("loadfile", url);
            await SendCommand("set_property", "pause", false);
        }

        public static async Task Play()
        {
            await SendCommand("set_property", "pause", false);
        }

        public static async Task Pause()
        {
            await SendCommand("set_property", "pause", true);
        }

        public static async Task Seek(double time)
        {
            await SendCommand("seek", time, "absolute");
        }

        public static async Task SetVolume(double volume) // 0-100
        {
            await SendCommand("set_property", "volume", volume);
        }

        private static async Task SendCommand(string command, params object[] args)
        {
            if (_pipeWriter == null) return;

            var cmdObj = new { command = new List<object> { command }.Concat(args) };
            string json = JsonSerializer.Serialize(cmdObj);

            try
            {
                await _pipeWriter.WriteLineAsync(json);
                await _pipeWriter.FlushAsync();
            }
            catch (Exception ex)
            {
                Log.Warning($"Failed to send MPV command: {ex.Message}");
            }
        }

        private static async Task ConnectAndListenIpc(CancellationToken token)
        {
            // Wait for MPV to create the pipe
            int retries = 0;
            while (retries < 10 && !token.IsCancellationRequested)
            {
                try
                {
                    _pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
                    await _pipeClient.ConnectAsync(1000, token);
                    _pipeReader = new StreamReader(_pipeClient);
                    _pipeWriter = new StreamWriter(_pipeClient);
                    Log.Information("Connected to MPV IPC.");

                    // Subscribe to properties
                    await SendCommand("observe_property", 1, "time-pos");
                    await SendCommand("observe_property", 2, "duration");
                    await SendCommand("observe_property", 3, "pause");
                    await SendCommand("observe_property", 4, "volume");
                    break;
                }
                catch
                {
                    retries++;
                    await Task.Delay(500, token);
                }
            }

            if (_pipeClient == null || !_pipeClient.IsConnected)
            {
                Log.Error("Failed to connect to MPV IPC.");
                return;
            }

            try
            {
                while (!token.IsCancellationRequested && _pipeClient.IsConnected)
                {
                    string? line = await _pipeReader!.ReadLineAsync();
                    if (line == null) break;

                    ProcessIpcMessage(line);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"MPV IPC loop error: {ex.Message}");
            }
        }

        private static void ProcessIpcMessage(string json)
        {
            try
            {
                var node = JsonNode.Parse(json);
                if (node == null) return;

                if (node["event"]?.GetValue<string>() == "property-change")
                {
                    string name = node["name"]?.GetValue<string>() ?? "";
                    var value = node["data"];

                    if (value == null) return;

                    // Broadcast to frontend
                    _ = MessageService.SendFrontendMessage("MpvEvent", new { name, value });
                }
            }
            catch
            {
                // Ignore parse errors
            }
        }
    }
}
