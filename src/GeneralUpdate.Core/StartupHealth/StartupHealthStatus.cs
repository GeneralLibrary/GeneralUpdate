namespace GeneralUpdate.Core.StartupHealth;

/// <summary>Outcome of an explicitly monitored startup attempt.</summary>
public enum StartupHealthStatus
{
    Pending = 0,
    Healthy = 1,
    Failed = 2,
    ProcessExited = 3,
    TimedOut = 4
}
