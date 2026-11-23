using GeneralUpdate.Core.StartupHealth;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download.Reporting;
using GeneralUpdate.Core.Strategy;
using System.Diagnostics;
using System.Reflection;

if (args.Length == 3 && args[0] == "attempt-apply-ordered")
{
    var bootstrap = new GeneralUpdateBootstrap();
    var context = (UpdateContext)typeof(GeneralUpdateBootstrap)
        .GetField("_configInfo", BindingFlags.Instance | BindingFlags.NonPublic)!
        .GetValue(bootstrap)!;
    context.LaunchClientAfterUpdate = false;
    using var client = new HttpClient();
    var strategy = new UpdateStrategy
    {
        Reporter = new OrderedUpdateReporter(args[1], client, args[2], "test-tenant/database", true)
    };
    strategy.Create(context);
    await strategy.ExecuteAsync();
    Console.WriteLine("applied");
    return;
}

if (args.Length == 3 && args[0] == "attempt-replay-ordered")
{
    using var client = new HttpClient();
    await new OrderedUpdateReporter(args[1], client, args[2], "test-tenant/database", true).ReplayAsync();
    Console.WriteLine("replayed");
    return;
}

if (args.Length == 1 && args[0] == "attempt-contract")
{
    try
    {
        var bootstrap = new GeneralUpdateBootstrap();
        var context = (UpdateContext)typeof(GeneralUpdateBootstrap)
            .GetField("_configInfo", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(bootstrap)!;
        Console.WriteLine(context.DurableUpdateAttemptId?.ToString("D") ?? "legacy");
    }
    catch (InvalidDataException)
    {
        Console.WriteLine("rejected");
    }
    return;
}

if (args.Length == 1 && args[0] == "attempt-apply")
{
    try
    {
        var bootstrap = new GeneralUpdateBootstrap();
        var context = (UpdateContext)typeof(GeneralUpdateBootstrap)
            .GetField("_configInfo", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(bootstrap)!;
        context.LaunchClientAfterUpdate = false;
        var statuses = new List<int>();
        var strategy = new UpdateStrategy { Reporter = new ProbeReporter(statuses) };
        strategy.Create(context);
        await strategy.ExecuteAsync();
        Console.WriteLine($"{context.DurableUpdateAttemptId:D}|{string.Join(",", statuses)}");
    }
    catch (InvalidDataException)
    {
        Console.WriteLine("rejected");
    }
    return;
}

if (args.Length == 3 && args[0] == "attempt-read")
{
    var record = new DurableUpdateAttemptStore(args[1]).GetAttempt(Guid.Parse(args[2]));
    Console.WriteLine($"{record.Status}|{record.SnapshotPath}|{record.Fence}");
    return;
}

if (args.Length == 4 && args[0] == "attempt-claim")
{
    try
    {
        var claim = new DurableUpdateAttemptStore(args[1]).ClaimRecovery(Guid.Parse(args[2]), long.Parse(args[3]));
        Console.WriteLine($"claimed|{claim.Fence}|{claim.ClaimId}");
    }
    catch (InvalidOperationException)
    {
        Console.WriteLine("stale");
    }
    return;
}

if (args.Length == 5 && args[0] == "attempt-bind-hold")
{
    var installPath = args[1];
    var attemptId = Guid.Parse(args[2]);
    var sourcePath = args[3];
    var readyPath = args[4];
    var snapshotPath = Path.Combine(installPath, StorageManager.BackupRootDirectory,
        StorageManager.GetBackupDirectoryName() + "-" + attemptId.ToString("N"));
    var store = new DurableUpdateAttemptStore(installPath);
    store.Begin(attemptId);
    store.ReserveSnapshot(attemptId, snapshotPath);
    StorageManager.Backup(sourcePath, snapshotPath, Array.Empty<string>(), createOnly: true);
    store.BindSnapshot(attemptId, snapshotPath);
    File.WriteAllText(readyPath, "bound");
    Thread.Sleep(Timeout.Infinite);
    return;
}

if (args.Length == 3 && args[0] == "native-handoff")
{
    var handoff = new StartupHealthHandoff(args[1]);
    var nativeId = Guid.NewGuid();
    handoff.Prepare(nativeId, DateTimeOffset.UtcNow.AddMinutes(1));
    var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
    start.ArgumentList.Add("business-ready");
    start.ArgumentList.Add(args[2]);
    using var child = handoff.Start(nativeId, start);
    if (await handoff.WaitAsync(nativeId, child) != StartupHealthStatus.Healthy)
        throw new InvalidOperationException("Native handoff failed.");
    Console.WriteLine("healthy");
    return;
}

if (args.Length == 4 && args[0] == "handoff-prepare")
{
    new StartupHealthHandoff(args[1]).Prepare(Guid.Parse(args[2]), DateTimeOffset.UtcNow.AddSeconds(30));
    File.WriteAllText(args[3], "prepared");
    Thread.Sleep(Timeout.Infinite);
    return;
}

if (args.Length == 7 && args[0] == "handoff-host")
{
    var handoff = new StartupHealthHandoff(args[1]);
    var handoffId = Guid.Parse(args[2]);
    var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
    start.ArgumentList.Add("exec");
    start.ArgumentList.Add("--runtimeconfig");
    start.ArgumentList.Add(args[5]);
    start.ArgumentList.Add("--depsfile");
    start.ArgumentList.Add(args[6]);
    start.ArgumentList.Add(args[4]);
    start.ArgumentList.Add("wait");
    using var child = handoff.Start(handoffId, start);
    File.WriteAllText(args[3], child.Id.ToString());
    await handoff.WaitAsync(handoffId, child);
    return;
}

if (args.Length == 1 && args[0] == "disabled")
{
    if (StartupHealthProcess.ConfirmCurrentProcess())
        throw new InvalidOperationException("Monitoring was unexpectedly enabled.");
    return;
}

if (args.Length == 7 && args[0] == "host")
{
    var host = new StartupHealthProcess(args[1]);
    var attempt = Guid.Parse(args[2]);
    var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
    start.ArgumentList.Add("exec");
    start.ArgumentList.Add("--runtimeconfig");
    start.ArgumentList.Add(args[5]);
    start.ArgumentList.Add("--depsfile");
    start.ArgumentList.Add(args[6]);
    start.ArgumentList.Add(args[4]);
    start.ArgumentList.Add("wait");
    using var child = host.Start(attempt, start);
    File.WriteAllText(args[3], child.Id.ToString());
    await host.WaitAsync(attempt, child);
    return;
}

if (args.Length == 2 && args[0] == "business-ready")
{
    if (!File.Exists(args[1]) || File.ReadAllText(args[1]) != "ready")
    {
        Console.Error.WriteLine("The business readiness check failed.");
        Environment.ExitCode = 1;
        return;
    }
    if (!StartupHealthProcess.ConfirmCurrentProcess())
        throw new InvalidOperationException("Readiness confirmation was not recorded.");
    return;
}

if (args.Length == 1 && args[0] == "exit")
    return;

if (args.Length == 1 && args[0] == "wait")
{
    Thread.Sleep(Timeout.Infinite);
    return;
}

var root = args[0];
if (args.Length == 2 && args[1] == "roundtrip")
{
    var store = new StartupHealthStore(root);
    var attempt = store.Begin(DateTimeOffset.UtcNow.AddMinutes(1));
    Environment.SetEnvironmentVariable(StartupHealthProcess.DirectoryVariable, root);
    Environment.SetEnvironmentVariable(StartupHealthProcess.AttemptVariable, attempt.ToString("D"));
    if (!StartupHealthProcess.ConfirmCurrentProcess() || new StartupHealthStore(root).GetStatus(attempt) != StartupHealthStatus.Healthy)
        throw new InvalidOperationException("Startup health roundtrip failed.");
    Console.WriteLine("healthy");
    return;
}

var id = Guid.Parse(args[1]);
if (args[2] == "status")
{
    Console.WriteLine(new StartupHealthStore(root).GetStatus(id));
}
else if (args[2] == "hold-lock")
{
    using var held = new FileStream(Path.Combine(root, id.ToString("N"), "lock"),
        FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    File.WriteAllText(args[3], "ready");
    Thread.Sleep(Timeout.Infinite);
}
else
{
    var store = new StartupHealthStore(root);
    var changed = args[2] == "healthy"
        ? store.ConfirmHealthy(id)
        : store.ReportFailure(id, StartupHealthStatus.Failed);
    Console.WriteLine(changed ? "changed" : "unchanged");
}

sealed class ProbeReporter : IUpdateReporter
{
    private readonly List<int> _statuses;

    public ProbeReporter(List<int> statuses) => _statuses = statuses;

    public Task ReportAsync(UpdateReport report, CancellationToken token = default)
    {
        _statuses.Add(report.Status);
        return Task.CompletedTask;
    }
}
