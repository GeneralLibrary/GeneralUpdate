using GeneralUpdate.Core.StartupHealth;
using System.Diagnostics;

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
