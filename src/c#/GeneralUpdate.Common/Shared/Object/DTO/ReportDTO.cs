using System.Text.Json.Serialization;

namespace GeneralUpdate.Common.Shared.Object;

public class ReportDTO
{
    /// <summary>
    /// 记录id
    /// </summary>
    [JsonPropertyName("recordId")]
    public int RecordId { get; set; }

    /// <summary>
    /// 更新状态
    /// </summary>
    [JsonPropertyName("status")]
    public int Status { get; set; }

    /// <summary>
    /// 1升级 2推送
    /// </summary>
    [JsonPropertyName("type")]
    public int Type { get; set; }
}