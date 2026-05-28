using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core;

namespace GeneralUpdate.Core.Download.Reporting;

/// <summary>
/// 更新状态报告器接口，用于向远程服务器（兼容 GeneralSpacestation API）报告更新生命周期事件。
/// </summary>
/// <remarks>
/// <para>
/// 实现此接口的类负责将更新过程中的状态变化上报到远程服务，以便跟踪更新进度和结果。
/// 典型的更新状态包括：
/// <list type="bullet">
///   <item><see cref="UpdateStatus.Updating"/> — 更新正在进行中。</item>
///   <item><see cref="UpdateStatus.Success"/> — 更新成功完成。</item>
///   <item><see cref="UpdateStatus.Failure"/> — 更新失败。</item>
/// </list>
/// </para>
/// <para>
/// 默认实现为 <see cref="NoOpUpdateReporter"/>（空操作），当未配置 ReportUrl 时使用。
/// 标准实现为 <see cref="HttpUpdateReporter"/>，通过 HTTP POST 将更新状态发送到指定端点。
/// </para>
/// </remarks>
public interface IUpdateReporter
{
    /// <summary>
    /// 异步报告更新状态到远程服务器。
    /// </summary>
    /// <param name="report">包含记录 ID、状态码和类型的更新报告数据。</param>
    /// <param name="token">用于取消报告操作的取消令牌。</param>
    /// <returns>表示异步操作的任务。</returns>
    Task ReportAsync(UpdateReport report, CancellationToken token = default);
}

/// <summary>
/// 更新状态枚举，匹配 GeneralSpacestation API 的 ReportDTO 协定。
/// 1 = 正在更新，2 = 成功，3 = 失败。
/// </summary>
public enum UpdateStatus { Updating = 1, Success = 2, Failure = 3 }

/// <summary>
/// 与 GeneralSpacestation 兼容的更新报告记录。
/// 包含版本验证返回的记录 ID（RecordId）、状态码（Status）和类型（Type）。
/// </summary>
/// <param name="RecordId">版本验证时返回的记录 ID，用于标识本次更新记录。</param>
/// <param name="Status">更新状态码：1=正在更新，2=成功，3=失败。默认为 1（正在更新）。</param>
/// <param name="Type">更新类型：1=升级，2=推送。默认为 1（升级）。</param>
/// <remarks>
/// 此记录序列化为 JSON 时使用 camelCase 命名策略。
/// 示例 JSON：{"recordId": 123, "status": 1, "type": 1}
/// </remarks>
public record UpdateReport(int RecordId, int Status = 1, int Type = 1);

/// <summary>
/// 基于 HTTP POST 的更新状态报告器，将 <see cref="UpdateReport"/> 序列化为 JSON
/// 并发送到指定的远程端点，格式与 GeneralSpacestation ReportDTO 兼容。
/// </summary>
/// <remarks>
/// <para>
/// 此类实现了 <see cref="IUpdateReporter"/> 接口，是标准的 HTTP 更新状态上报实现。
/// </para>
/// <para>
/// 工作流程：
/// <list type="number">
///   <item>将 <c>UpdateReport</c> 序列化为 JSON 字符串（使用 camelCase 命名策略）。</item>
///   <item>创建 HTTP POST 请求，设置 Content-Type 为 application/json。</item>
///   <item>发送请求到配置的报告 URL。</item>
///   <item>如果请求失败（如网络错误），记录警告日志但不抛出异常，避免影响更新主流程。</item>
/// </list>
/// </para>
/// </remarks>
public class HttpUpdateReporter : IUpdateReporter
{
    private readonly HttpClient _client;
    private readonly string _reportUrl;

    /// <summary>
    /// 使用指定的 HTTP 客户端和报告 URL 初始化 HTTP 报告器。
    /// </summary>
    /// <param name="client">用于发送 HTTP 请求的 <see cref="HttpClient"/> 实例。</param>
    /// <param name="reportUrl">接收更新状态报告的远程 URL。</param>
    public HttpUpdateReporter(HttpClient client, string reportUrl)
    {
        _client = client;
        _reportUrl = reportUrl;
    }

    public async Task ReportAsync(UpdateReport report, CancellationToken token = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            using var request = new HttpRequestMessage(HttpMethod.Post, _reportUrl);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            await _client.SendAsync(request, token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"Report failed: {ex.Message}");
        }
    }
}

/// <summary>
/// 空操作（No-op）更新状态报告器，当未配置 ReportUrl 时使用。
/// 所有报告操作都立即返回已完成的任务，不执行任何实际操作。
/// </summary>
/// <remarks>
/// 此类实现了空对象模式（Null Object Pattern），
/// 避免在使用方代码中需要判空处理。
/// 当不需要向服务器报告更新状态时（如本地测试或未配置报告端点），使用此实现。
/// </remarks>
public class NoOpUpdateReporter : IUpdateReporter
{
    public Task ReportAsync(UpdateReport report, CancellationToken token = default)
        => Task.CompletedTask;
}
