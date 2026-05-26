using System.Text.Json.Serialization;

namespace GeneralUpdate.Bowl.Configuration;

/// <summary>
/// Minimal ProcessInfo for Bowl — only the fields needed for crash monitoring and rollback.
/// </summary>
public class ProcessInfo
{
    [JsonPropertyName("AppName")]
    public string AppName { get; set; } = string.Empty;

    [JsonPropertyName("InstallPath")]
    public string InstallPath { get; set; } = string.Empty;

    [JsonPropertyName("LastVersion")]
    public string LastVersion { get; set; } = string.Empty;
}
