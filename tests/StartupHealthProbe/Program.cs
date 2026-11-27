using GeneralUpdate.Core.StartupHealth;

var root = args[0];
if (args.Length == 2 && args[1] == "roundtrip")
{
    var store = new StartupHealthStore(root);
    var attempt = store.Begin(DateTimeOffset.UtcNow.AddMinutes(1));
    if (!store.ConfirmHealthy(attempt) || new StartupHealthStore(root).GetStatus(attempt) != StartupHealthStatus.Healthy)
        throw new InvalidOperationException("Startup health roundtrip failed.");
    Console.WriteLine("healthy");
    return;
}

var id = Guid.Parse(args[1]);
if (args[2] == "hold-lock")
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
