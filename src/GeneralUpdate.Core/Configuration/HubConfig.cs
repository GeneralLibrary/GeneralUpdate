using System;
using System.Collections.Generic;

namespace GeneralUpdate.Core.Configuration;

public sealed class HubConfig
{
    public string Url { get; set; } = string.Empty;
    public string ValidationUrl { get; set; } = string.Empty;
    public string? AccessToken { get; set; }
    public string? AppKey { get; set; }
    public string TenantHeaderName { get; set; } = "X-Tenant-Id";
    public bool ForwardTenantHeaderToHub { get; set; }
    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);
    public int MaxReconnectAttempts { get; set; } = 10;
    public int QueueCapacity { get; set; } = 16;
    public List<string> AllowedDownloadOrigins { get; set; } = new();
}
