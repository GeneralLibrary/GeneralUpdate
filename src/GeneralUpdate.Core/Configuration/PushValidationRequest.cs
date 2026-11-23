using System.Text.Json.Serialization;

namespace GeneralUpdate.Core.Configuration;

public sealed class PushValidationRequest
{
    [JsonPropertyName("recordId")]
    public int RecordId { get; set; }

    [JsonPropertyName("appKey")]
    public string AppKey { get; set; } = string.Empty;

    [JsonPropertyName("packetHash")]
    public string PacketHash { get; set; } = string.Empty;
}

public sealed class PushValidationResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("body")]
    public bool Body { get; set; }
}
