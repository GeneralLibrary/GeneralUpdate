namespace GeneralUpdate.Core.Configuration;

/// <summary>Opt-in integration with the separately deployed Bowl v1 host.</summary>
public sealed class BowlOptions
{
    /// <summary>False preserves operation without a Bowl deployment; true requires a successful readiness handshake.</summary>
    public bool Enabled { get; set; }
    /// <summary>Absolute path to the actual host executable, outside every update/backup tree; not a launcher or library.</summary>
    public string ExecutablePath { get; set; } = string.Empty;
    /// <summary>Maximum seconds to wait for a matching, live host before aborting without applying application files.</summary>
    public int ReadyTimeoutSeconds { get; set; } = 10;
    /// <summary>Observe process survival, not business health, after launch.</summary>
    public bool VerifyLaunch { get; set; } = true;
    /// <summary>Continuous survival observation window in seconds; ignored for filesOnly attempts.</summary>
    public int HealthTimeoutSeconds { get; set; } = 5;
    /// <summary>Total attempt deadline delegated to Bowl, in seconds; must exceed the readiness timeout.</summary>
    public int UpdateTimeoutSeconds { get; set; } = 600;
    /// <summary>Allow Bowl-owned rollback only when backups are enabled and the specified snapshot actually exists.</summary>
    public bool AutoRollback { get; set; }
    /// <summary>Name of a host-provisioned environment variable; never the credential itself.</summary>
    public string? CredentialEnvironmentVariable { get; set; }
}
