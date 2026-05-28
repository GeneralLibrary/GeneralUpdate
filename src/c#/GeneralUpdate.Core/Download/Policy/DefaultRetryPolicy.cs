using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.Download.Abstractions;
using GeneralUpdate.Core;

namespace GeneralUpdate.Core.Download.Policy;

/// <summary>
/// 默认的下载重试策略，基于指数退避算法。
/// 对于可恢复的临时故障（超时、网络 I/O 错误、5xx 服务器错误）进行重试。
/// 对于永久性故障（4xx 客户端错误、SSL/认证错误）不进行重试。
/// </summary>
/// <remarks>
/// <para>
/// 此类实现了 <see cref="IDownloadPolicy"/> 接口，为下载操作提供可配置的重试机制。
/// </para>
/// <para>
/// 重试策略特性：
/// <list type="bullet">
///   <item><term>指数退避</term><description>每次重试的延迟时间按指数增长。
///        延迟计算公式：<c>initialDelay * backoffMultiplier^attempt</c>。
///        例如，初始延迟 1 秒、倍率 2.0 时，重试间隔为 1s、2s、4s、8s……</description></item>
///   <item><term>可配置最大重试次数</term><description>默认最多重试 3 次（即总共最多执行 3 次尝试）。</description></item>
///   <item><term>可重试异常判断</term><description>通过 <c>IsRetryable</c> 方法精确判断哪些异常值得重试，
///        避免对 4xx 客户端错误等永久故障进行无效重试。</description></item>
///   <item><term>取消支持</term><description>重试间隔期间会响应 <c>CancellationToken</c> 的取消请求。</description></item>
/// </list>
/// </para>
/// <para>
/// 可重试的异常类型：
/// <list type="bullet">
///   <item><c>TaskCanceledException</c> — 任务被取消（可能是超时引起）。</item>
///   <item><c>TimeoutException</c> — 操作超时。</item>
///   <item><c>IOException</c> — 网络 I/O 错误。</item>
///   <item>包含 timeout/500/502/503/504 状态码的 <c>HttpRequestException</c>。</item>
/// </list>
/// </para>
/// </remarks>
public class DefaultRetryPolicy : IDownloadPolicy
{
    private readonly int _maxRetries;
    private readonly TimeSpan _initialDelay;
    private readonly double _backoffMultiplier;

    /// <summary>
    /// 使用指定的重试次数、初始延迟和退避倍率初始化重试策略。
    /// </summary>
    /// <param name="maxRetries">最大重试次数（包含首次尝试）。默认值为 3。</param>
    /// <param name="initialDelay">每次重试前的初始延迟时间。默认值为 1 秒。</param>
    /// <param name="backoffMultiplier">退避倍率，每次重试的延迟时间按此倍率增长。默认值为 2.0。</param>
    public DefaultRetryPolicy(int maxRetries = 3, TimeSpan? initialDelay = null, double backoffMultiplier = 2.0)
    {
        _maxRetries = maxRetries;
        _initialDelay = initialDelay ?? TimeSpan.FromSeconds(1);
        _backoffMultiplier = backoffMultiplier;
    }

    /// <summary>
    /// 异步执行指定的操作，并在发生可重试异常时根据指数退避策略进行重试。
    /// </summary>
    /// <typeparam name="T">操作返回的类型。</typeparam>
    /// <param name="action">要执行的操作，接受 <see cref="CancellationToken"/> 并返回 <see cref="Task{T}"/>。</param>
    /// <param name="token">用于取消操作的取消令牌。</param>
    /// <returns>操作执行结果。</returns>
    /// <exception cref="OperationCanceledException">当操作通过取消令牌被取消时抛出（此异常不会被重试）。</exception>
    /// <remarks>
    /// <para>
    /// 执行流程：
    /// </para>
    /// <list type="number">
    ///   <item>执行传入的操作。</item>
    ///   <item>如果操作成功，直接返回结果。</item>
    ///   <item>如果操作抛出可重试异常且尚未达到最大重试次数，记录警告日志，等待退避延迟后重试。</item>
    ///   <item>如果操作抛出不可重试异常或已达到最大重试次数，异常会向上传播（不被捕获）。</item>
    /// </list>
    /// </remarks>
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken token = default)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                return await action(token).ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < _maxRetries - 1 && IsRetryable(ex))
            {
                GeneralTracer.Warn($"Download attempt {attempt + 1}/{_maxRetries} failed, retrying. {ex.Message}");
                var delay = TimeSpan.FromMilliseconds(_initialDelay.TotalMilliseconds * Math.Pow(_backoffMultiplier, attempt));
                await Task.Delay(delay, token).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// 判断指定异常是否可重试。
    /// </summary>
    /// <param name="ex">要检查的异常。</param>
    /// <returns>如果异常属于可重试类型（超时、网络 I/O、5xx 服务器错误）则返回 true；否则返回 false。</returns>
    /// <remarks>
    /// <para>以下异常被认为是可重试的：</para>
    /// <list type="bullet">
    ///   <item><c>TaskCanceledException</c> — 任务被取消（通常由超时引起）。</item>
    ///   <item><c>TimeoutException</c> — 操作超时。</item>
    ///   <item><c>IOException</c> — 网络 I/O 错误。</item>
    ///   <item>包含 "timeout"、"500"、"502"、"503" 或 "504" 的 <c>HttpRequestException</c>。</item>
    /// </list>
    /// <para>以下异常被认为是不可重试的：</para>
    /// <list type="bullet">
    ///   <item><c>OperationCanceledException</c> — 用户主动取消。</item>
    ///   <item>4xx 客户端错误（由 <c>HttpRequestException</c> 抛出时不会匹配上述条件）。</item>
    /// </list>
    /// </remarks>
    private static bool IsRetryable(Exception ex)
    {
        if (ex is OperationCanceledException) return false;
        if (ex is TaskCanceledException or TimeoutException) return true;
        if (ex is IOException) return true;
        if (ex is HttpRequestException hre)
        {
            var s = hre.Message ?? "";
            return s.Contains("timeout", StringComparison.OrdinalIgnoreCase)
                || s.Contains("timed out", StringComparison.OrdinalIgnoreCase)
                || s.Contains("500") || s.Contains("502")
                || s.Contains("503") || s.Contains("504");
        }
        return false;
    }
}
