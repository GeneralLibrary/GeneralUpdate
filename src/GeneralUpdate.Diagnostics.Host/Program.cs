using System.Globalization;
using System.Runtime.InteropServices;
using GeneralUpdate.Core.Diagnostics;

try
{
    var enabled = Environment.GetEnvironmentVariable("GENERALUPDATE_DIAGNOSTICS_ENABLED");
    if (enabled == null || enabled.Equals("false", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Disabled");
        return 0;
    }
    if (!enabled.Equals("true", StringComparison.OrdinalIgnoreCase))
        throw new ArgumentException("GENERALUPDATE_DIAGNOSTICS_ENABLED must be true or false.");
    if (args.Length != 2)
        throw new ArgumentException("Usage: GeneralUpdate.Diagnostics.Host <fail-directory> <https-artifact-endpoint>");

    var options = new DiagnosticOutboxHostOptions
    {
        Enabled = true,
        FailDirectory = args[0],
        Endpoint = new Uri(args[1], UriKind.Absolute),
        TenantId = Environment.GetEnvironmentVariable("GENERALUPDATE_DIAGNOSTICS_TENANT") ?? string.Empty,
        AppKey = Environment.GetEnvironmentVariable("GENERALUPDATE_DIAGNOSTICS_APP_KEY") ?? string.Empty,
        ScanInterval = Seconds("GENERALUPDATE_DIAGNOSTICS_SCAN_SECONDS", 60),
        UploadTimeout = Seconds("GENERALUPDATE_DIAGNOSTICS_UPLOAD_SECONDS", 30)
    };
    using var stop = new CancellationTokenSource();
    using var terminate = OperatingSystem.IsWindows() ? null :
        PosixSignalRegistration.Create(PosixSignal.SIGTERM, context => { context.Cancel = true; stop.Cancel(); });
    ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; stop.Cancel(); };
    Console.CancelKeyPress += cancel;
    try
    {
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false };
        using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var host = new DiagnosticOutboxHost(options, e =>
        {
            var line = $"{DateTimeOffset.UtcNow:O} {e.Status} {e.Path}";
            if (e.Error == null) Console.WriteLine(line);
            else Console.Error.WriteLine($"{line} {e.Error.GetType().Name}: {e.Error.Message}");
        }, http);
        await host.StartAsync(stop.Token);
        return 0;
    }
    finally { Console.CancelKeyPress -= cancel; }
}
catch (Exception error)
{
    Console.Error.WriteLine($"{error.GetType().Name}: {error.Message}");
    return 1;
}

static TimeSpan Seconds(string variable, int fallback)
{
    var text = Environment.GetEnvironmentVariable(variable);
    if (text == null) return TimeSpan.FromSeconds(fallback);
    if (!double.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var seconds) ||
        double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0 || seconds > 86400)
        throw new ArgumentException($"{variable} must be a positive number of seconds no greater than 86400.");
    return TimeSpan.FromSeconds(seconds);
}
