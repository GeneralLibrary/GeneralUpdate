using System;

namespace GeneralUpdate.Core.Configuration;

public sealed class HubConfig
{
    public string Url { get; set; } = string.Empty;
    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);
    public int MaxReconnectAttempts { get; set; } = 10;
}
