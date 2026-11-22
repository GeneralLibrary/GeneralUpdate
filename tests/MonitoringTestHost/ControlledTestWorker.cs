using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using GeneralUpdate.Core.Compress;

// Test-only adapter; a PID check and Job are not production authorization.
internal static class ControlledTestWorker
{
    internal static async Task RunAsync(string[] args)
    {
        if (args.Length != 7 || !OperatingSystem.IsWindows())
            throw new ArgumentException("Invalid controlled test worker invocation.");
        if (!int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out int hostPid) || hostPid <= 0 ||
            !DateTimeOffset.TryParseExact(args[3], "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var hostStart) ||
            !Guid.TryParseExact(args[5], "D", out var attempt) || attempt.ToString("D") != args[5] ||
            args[6] is not ("success" or "fail-after-first" or "fail-after-second"))
            throw new ArgumentException("Invalid controlled test worker identity or mode.");
        string root = Path.GetFullPath(args[4]);
        string expected = Path.Combine(Path.GetTempPath(), "BowlControlledIntegration", args[5]);
        if (!string.Equals(root, Path.GetFullPath(expected), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Controlled test worker requires the isolated test root.");

        using var pipe = new NamedPipeClientStream(".", args[1], PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(10000);
        if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle, out uint serverPid) || serverPid != hostPid)
            throw new InvalidOperationException("Pipe server identity mismatch.");
        using var host = Process.GetProcessById(hostPid);
        if (host.HasExited || host.StartTime.ToUniversalTime() != hostStart.UtcDateTime)
            throw new InvalidOperationException("Pipe server start time mismatch.");
        using var reader = new StreamReader(pipe, new UTF8Encoding(false, true), false, 1024, true);
        using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, true) { AutoFlush = true, NewLine = "\n" };
        await writer.WriteLineAsync("READY");
        if (await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)) != "GO")
            throw new InvalidOperationException("Test supervisor did not authorize the fixture.");
        int finished = 0;
        var watcher = Task.Run(async () =>
        {
            try { await reader.ReadLineAsync(); }
            catch (IOException) { }
            if (Volatile.Read(ref finished) == 0)
                Environment.FailFast("Test supervisor connection closed.");
        });

        using var configuration = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "test-package.json")));
        var url = new Uri(configuration.RootElement.GetProperty("url").GetString()!, UriKind.Absolute);
        if (url.Scheme != "http" || url.Host != "127.0.0.1")
            throw new InvalidDataException("Test package URL must be IPv4 loopback.");
        using var http = new HttpClient();
        var bytes = await http.GetByteArrayAsync(url);
        var archive = Path.Combine(root, "download.zip");
        await File.WriteAllBytesAsync(archive, bytes);
        using var staged = FullZipStager.Stage(archive, Path.Combine(root, "stage"),
            configuration.RootElement.GetProperty("sha256").GetString()!, 10, 1024 * 1024, 2 * 1024 * 1024);
        var plan = ControlledFullZipPrepare.Prepare(staged, Path.Combine(root, "install"),
            Path.Combine(root, "state"), Path.Combine(root, "backup"), ["data"]);
        ControlledFullZipApply.Apply(plan, afterIntent: staged.RetainForRecovery, afterWrite: i =>
        {
            if (i == 0 && File.Exists(Path.Combine(root, "test-pause-after-first")))
            {
                File.WriteAllText(Path.Combine(root, "test-first-write-complete"), "ready");
                Thread.Sleep(Timeout.Infinite);
            }
            if ((args[6] == "fail-after-first" && i == 0) || (args[6] == "fail-after-second" && i == 1))
                throw new InvalidOperationException("Injected controlled test worker failure.");
        });
        await writer.WriteLineAsync("DONE");
        Volatile.Write(ref finished, 1);
        GC.KeepAlive(watcher);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetNamedPipeServerProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe, out uint serverPid);
}
