using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core;
using GeneralUpdate.Core.JsonContext;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Security;

namespace GeneralUpdate.Core.Network
{
    /// <summary>
    /// 版本服务，提供与更新服务器的 HTTP 通信能力，包括版本校验和状态上报。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 该类是 GeneralUpdate 框架的 HTTP 通信层，其核心设计要点如下：
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <description>使用静态共享的 <see cref="HttpClient"/> 实例（<c>_sharedClient</c>），避免套接字耗尽，
    ///     并通过 <see cref="SetSslValidationPolicy"/> 支持可配置的 SSL 证书验证策略。</description>
    ///   </item>
    ///   <item>
    ///     <description>提供两套静态 API（<see cref="Validate(string, string, AppType, string, PlatformType, string, string, string, CancellationToken)"/>
    ///     和 <see cref="Report(string, int, int, int?, string, string, CancellationToken)"/>），
    ///     内部自动创建实例并调用对应的异步方法。这些静态方式为向后兼容而保留。</description>
    ///   </item>
    ///   <item>
    ///     <description>支持可插拔的认证提供者（<see cref="IHttpAuthProvider"/>），
    ///     内置支持 Bearer Token、API Key、HMAC 等认证方式，也可通过 <see cref="HttpAuthProviderFactory"/> 自定义扩展。</description>
    ///   </item>
    ///   <item>
    ///     <description>具备指数退避重试机制：在 <see cref="PostAsync{T}"/> 中捕获可重试的异常，
    ///     等待时间按 2^attempt * 1000 毫秒递增。</description>
    ///   </item>
    ///   <item>
    ///     <description>支持全局 SSL 策略（<see cref="SetSslValidationPolicy"/>）和全局认证提供者
    ///     （<see cref="SetDefaultAuthProvider"/>）配置。当设置了全局认证提供者时，
    ///     它将覆盖工厂方法 <see cref="HttpAuthProviderFactory.Create"/> 创建的认证实例。</description>
    ///   </item>
    /// </list>
    /// <para>
    /// 典型使用场景：
    /// <list type="bullet">
    ///   <item><description>启动时调用 <see cref="Validate(string, string, AppType, string, PlatformType, string, string, string, CancellationToken)"/>
    ///   检查服务器是否有新版本。</description></item>
    ///   <item><description>下载完成后调用 <see cref="Report(string, int, int, int?, string, string, CancellationToken)"/>
    ///   上报更新状态。</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public class VersionService
    {
        private static readonly HttpClient _sharedClient;
        private static ISslValidationPolicy _globalSslPolicy = new StrictSslValidationPolicy();
        private static IHttpAuthProvider? _globalAuthProvider;

        private readonly IHttpAuthProvider _auth;
        private readonly TimeSpan _timeout;
        private readonly int _maxRetries;

        /// <summary>
        /// 初始化 <see cref="VersionService"/> 的静态成员。
        /// </summary>
        /// <remarks>
        /// 创建带有自定义 SSL 验证回调的 <see cref="HttpClientHandler"/>，
        /// 并使用该 handler 初始化静态共享的 <see cref="HttpClient"/> 实例。
        /// SSL 验证逻辑委托给 <see cref="ISslValidationPolicy"/>，可通过 <see cref="SetSslValidationPolicy"/> 全局替换。
        /// </remarks>
        static VersionService()
        {
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = SharedCertValidation;
            _sharedClient = new HttpClient(handler, disposeHandler: false);
        }

        /// <summary>
        /// 设置全局 SSL 证书验证策略。
        /// </summary>
        /// <remarks>
        /// 该策略会影响所有 <see cref="VersionService"/> 实例的 HTTPS 请求。
        /// 默认使用 <see cref="StrictSslValidationPolicy"/>，即严格模式。
        /// 可通过传入自定义的 <see cref="ISslValidationPolicy"/> 实现来放宽或替换验证逻辑。
        /// </remarks>
        /// <param name="policy">SSL 验证策略实例。不能为 null。</param>
        /// <exception cref="ArgumentNullException"><paramref name="policy"/> 为 null 时抛出。</exception>
        public static void SetSslValidationPolicy(ISslValidationPolicy policy)
            => _globalSslPolicy = policy ?? throw new ArgumentNullException(nameof(policy));

        /// <summary>
        /// 设置全局默认的 HTTP 认证提供者。
        /// </summary>
        /// <remarks>
        /// 当设置了全局认证提供者后，所有通过静态 API（<see cref="Validate(string, string, AppType, string, PlatformType, string, string, string, CancellationToken)"/>
        /// 和 <see cref="Report(string, int, int, int?, string, string, CancellationToken)"/>）发起的请求将优先使用该提供者，
        /// 覆盖 <see cref="HttpAuthProviderFactory.Create"/> 所创建的认证实例。
        /// <para>
        /// 传入 null 可清除全局认证提供者，此时将回退到工厂方法创建的认证实例。
        /// </para>
        /// </remarks>
        /// <param name="provider">全局认证提供者实例，或 null 以清除全局配置。</param>
        public static void SetDefaultAuthProvider(IHttpAuthProvider? provider)
            => _globalAuthProvider = provider;

        private static bool SharedCertValidation(HttpRequestMessage m, X509Certificate2? c,
            X509Chain? ch, SslPolicyErrors e)
            => _globalSslPolicy.ValidateCertificate(c, ch, e);

        /// <summary>
        /// 初始化 <see cref="VersionService"/> 的新实例。
        /// </summary>
        /// <remarks>
        /// 实例方法（<see cref="ValidateAsync"/> 和 <see cref="ReportAsync"/>）使用该实例的认证提供者和超时设置。
        /// <paramref name="auth"/> 为 null 时，默认使用 <see cref="NoOpAuthProvider"/>（不执行任何认证）。
        /// </remarks>
        /// <param name="auth">HTTP 认证提供者。为 null 时使用 <see cref="NoOpAuthProvider"/>。</param>
        /// <param name="timeout">请求超时时间。为 null 时默认 30 秒。</param>
        /// <param name="maxRetries">最大重试次数，默认值为 3。</param>
        public VersionService(IHttpAuthProvider? auth = null, TimeSpan? timeout = null, int maxRetries = 3)
        {
            _auth = auth ?? new NoOpAuthProvider();
            _timeout = timeout ?? TimeSpan.FromSeconds(30);
            _maxRetries = maxRetries;
        }
        /// <summary>
        /// 向服务器上报指定记录的更新状态。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 这是一个向后兼容的静态便捷方法，内部自动创建 <see cref="VersionService"/> 实例并调用 <see cref="ReportAsync"/>。
        /// </para>
        /// <para>
        /// 执行流程：
        /// <list type="number">
        ///   <item><description>解析认证提供者：优先使用全局认证提供者（<see cref="SetDefaultAuthProvider"/>），否则通过
        ///   <see cref="HttpAuthProviderFactory.Create"/> 创建。</description></item>
        ///   <item><description>创建临时 <see cref="VersionService"/> 实例。</description></item>
        ///   <item><description>调用 <see cref="ReportAsync"/> 执行上报。</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        /// <param name="url">服务器 API 地址。</param>
        /// <param name="recordId">更新记录标识符。</param>
        /// <param name="status">当前状态码。</param>
        /// <param name="type">更新类型（可为 null）。</param>
        /// <param name="scheme">认证方案（如 "bearer"、"apikey"、"hmac"），用于创建认证提供者。当设置了全局认证提供者时此参数无效。</param>
        /// <param name="token">认证令牌或密钥，与 <paramref name="scheme"/> 配合使用。</param>
        /// <param name="ct">用于取消操作的 <see cref="CancellationToken"/>。</param>
        /// <returns>表示异步操作的任务。</returns>
        public static Task Report(string url, int recordId, int status, int? type,
            string scheme = null, string token = null, CancellationToken ct = default)
        {
            var a = _globalAuthProvider ?? HttpAuthProviderFactory.Create(scheme, token, null);
            return new VersionService(a).ReportAsync(url, recordId, status, type, ct);
        }

        /// <summary>
        /// 向服务器校验当前版本，查询是否有可用更新。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 这是推荐的强类型重载。内部自动创建 <see cref="VersionService"/> 实例并调用 <see cref="ValidateAsync"/>。
        /// </para>
        /// <para>
        /// 执行流程：
        /// <list type="number">
        ///   <item><description>解析认证提供者：优先使用全局认证提供者（<see cref="SetDefaultAuthProvider"/>），否则通过
        ///   <see cref="HttpAuthProviderFactory.Create"/> 创建。</description></item>
        ///   <item><description>创建临时 <see cref="VersionService"/> 实例。</description></item>
        ///   <item><description>构造包含版本、应用类型、平台等信息的请求参数。</description></item>
        ///   <item><description>通过 POST 请求将参数发送至服务器，反序列化响应为 <see cref="VersionRespDTO"/>。</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        /// <param name="url">服务器版本校验 API 地址。</param>
        /// <param name="version">当前客户端版本号。</param>
        /// <param name="appType">应用类型（如主程序、补丁等）。</param>
        /// <param name="appKey">应用密钥，用于服务端鉴权。</param>
        /// <param name="platform">目标平台（Windows、Linux、macOS 等）。</param>
        /// <param name="productId">产品标识符。</param>
        /// <param name="scheme">认证方案（如 "bearer"、"apikey"、"hmac"），用于创建认证提供者。当设置了全局认证提供者时此参数无效。</param>
        /// <param name="token">认证令牌或密钥，与 <paramref name="scheme"/> 配合使用。</param>
        /// <param name="ct">用于取消操作的 <see cref="CancellationToken"/>。</param>
        /// <returns>包含版本校验结果（如是否存在更新、下载地址等）的 <see cref="VersionRespDTO"/>。</returns>
        public static Task<VersionRespDTO> Validate(string url, string version,
            AppType appType, string appKey, PlatformType platform, string productId,
            string scheme = null, string token = null, CancellationToken ct = default)
        {
            var a = _globalAuthProvider ?? HttpAuthProviderFactory.Create(scheme, token, appKey);
            return new VersionService(a).ValidateAsync(url, version, (int)appType, appKey, (int)platform, productId, ct);
        }

        /// <summary>
        /// 向服务器校验当前版本（使用整数参数的向后兼容重载）。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 该重载将整数参数转换为对应的枚举类型后，委托给强类型重载 <see cref="Validate(string, string, AppType, string, PlatformType, string, string, string, CancellationToken)"/> 执行。
        /// 为保持与旧调用方的二进制兼容性而保留。
        /// </para>
        /// </remarks>
        /// <param name="url">服务器版本校验 API 地址。</param>
        /// <param name="version">当前客户端版本号。</param>
        /// <param name="appType">应用类型（整数形式，将转换为 <see cref="AppType"/>）。</param>
        /// <param name="appKey">应用密钥。</param>
        /// <param name="platform">目标平台（整数形式，将转换为 <see cref="PlatformType"/>）。</param>
        /// <param name="productId">产品标识符。</param>
        /// <param name="scheme">认证方案。</param>
        /// <param name="token">认证令牌或密钥。</param>
        /// <param name="ct">用于取消操作的 <see cref="CancellationToken"/>。</param>
        /// <returns>包含版本校验结果的 <see cref="VersionRespDTO"/>。</returns>
        public static Task<VersionRespDTO> Validate(string url, string version,
            int appType, string appKey, int platform, string productId,
            string scheme = null, string token = null, CancellationToken ct = default)
            => Validate(url, version, (AppType)appType, appKey, (PlatformType)platform, productId, scheme, token, ct);

        /// <summary>
        /// 异步上报更新记录的状态。
        /// </summary>
        /// <param name="url">服务器 API 地址。</param>
        /// <param name="recordId">更新记录标识符。</param>
        /// <param name="status">当前状态码。</param>
        /// <param name="type">更新类型（可为 null）。</param>
        /// <param name="t">用于取消操作的 <see cref="CancellationToken"/>。</param>
        /// <returns>表示异步操作的任务。</returns>
        private async Task ReportAsync(string url, int recordId, int status, int? type, CancellationToken t = default)
        {
            var p = new Dictionary<string, object> { ["recordId"] = recordId, ["status"] = status, ["type"] = type };
            await PostAsync<BaseResponseDTO<bool>>(url, p, ReportRespJsonContext.Default.BaseResponseDTOBoolean, t);
        }

        /// <summary>
        /// 异步校验版本，向服务器查询可用更新。
        /// </summary>
        /// <param name="url">服务器版本校验 API 地址。</param>
        /// <param name="v">当前客户端版本号。</param>
        /// <param name="at">应用类型的整数值。</param>
        /// <param name="appKey">应用密钥。</param>
        /// <param name="pf">平台类型的整数值。</param>
        /// <param name="pid">产品标识符。</param>
        /// <param name="t">用于取消操作的 <see cref="CancellationToken"/>。</param>
        /// <returns>包含版本校验结果的 <see cref="VersionRespDTO"/>。</returns>
        private async Task<VersionRespDTO> ValidateAsync(string url, string v, int at, string appKey, int pf, string pid,
            CancellationToken t = default)
        {
            var p = new Dictionary<string, object> { ["version"] = v, ["appType"] = at, ["appKey"] = appKey, ["platform"] = pf, ["productId"] = pid, ["upgradeMode"] = 1 };
            return await PostAsync<VersionRespDTO>(url, p, VersionRespJsonContext.Default.VersionRespDTO, t);
        }

        /// <summary>
        /// 执行带有指数退避重试机制的 HTTP POST 请求。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 本方法封装了重试逻辑，流程如下：
        /// </para>
        /// <list type="number">
        ///   <item>
        ///     <description>调用 <see cref="SendAsync{T}"/> 发送 POST 请求。</description>
        ///   </item>
        ///   <item>
        ///     <description>若请求成功，直接返回反序列化后的结果。</description>
        ///   </item>
        ///   <item>
        ///     <description>若抛出可重试的异常（参见 <see cref="IsRetryable"/>）且未达到最大重试次数，
        ///     则等待指数递增的时间（2^attempt * 1000 毫秒）后重试。</description>
        ///   </item>
        ///   <item>
        ///     <description>不可重试的异常（如 <see cref="OperationCanceledException"/>）会立即向上传播。</description>
        ///   </item>
        /// </list>
        /// <para>
        /// 重试等待期间会通过 <see cref="Task.Delay(TimeSpan, CancellationToken)"/> 释放线程，
        /// 并响应取消令牌。
        /// </para>
        /// </remarks>
        /// <typeparam name="T">响应数据的反序列化目标类型。</typeparam>
        /// <param name="url">请求的目标 URL。</param>
        /// <param name="p">POST 请求体参数字典。</param>
        /// <param name="ti">用于源代码生成器（source generator）的 JSON 类型信息元数据，可为 null（此时使用反射反序列化）。</param>
        /// <param name="t">用于取消操作的 <see cref="CancellationToken"/>。</param>
        /// <returns>反序列化后的响应数据。</returns>
        private async Task<T> PostAsync<T>(string url, Dictionary<string, object> p,
            JsonTypeInfo<T>? ti, CancellationToken t)
        {
            for (int attempt = 0; ; attempt++)
            {
                try { return await SendAsync<T>(url, p, ti, t).ConfigureAwait(false); }
                catch (Exception ex) when (attempt < _maxRetries - 1 && IsRetryable(ex))
                {
                    GeneralTracer.Warn($"HTTP attempt {attempt + 1}/{_maxRetries} failed, retrying. {ex.Message}");
                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 1000), t).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// 执行单个 HTTP POST 请求，包含认证注入和超时控制。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 本方法负责单次 HTTP 请求的全过程：
        /// </para>
        /// <list type="number">
        ///   <item>
        ///     <description>构造 <see cref="HttpRequestMessage"/>，设置 URL、方法（POST）和 Accept 头。</description>
        ///   </item>
        ///   <item>
        ///     <description>将参数字典序列化为 JSON 字符串，设置为请求内容。</description>
        ///   </item>
        ///   <item>
        ///     <description>调用 <see cref="IHttpAuthProvider.ApplyAuthAsync"/> 注入认证信息（如 Bearer Token）。</description>
        ///   </item>
        ///   <item>
        ///     <description>通过 <see cref="CancellationTokenSource.CreateLinkedTokenSource"/> 将传入的取消令牌与超时令牌关联，
        ///     确保超时或取消任一触发时请求立即中止。</description>
        ///   </item>
        ///   <item>
        ///     <description>使用静态共享的 <see cref="HttpClient"/> 发送请求，并调用 <c>EnsureSuccessStatusCode</c> 验证响应状态。</description>
        ///   </item>
        ///   <item>
        ///     <description>读取响应内容为字符串，并通过 <paramref name="ti"/> 或反射反序列化为目标类型 <typeparamref name="T"/>。</description>
        ///   </item>
        /// </list>
        /// </remarks>
        /// <typeparam name="T">响应数据的反序列化目标类型。</typeparam>
        /// <param name="url">请求的目标 URL。</param>
        /// <param name="p">POST 请求体参数字典。</param>
        /// <param name="ti">用于源代码生成器的 JSON 类型信息元数据，可为 null。</param>
        /// <param name="t">用于取消操作的 <see cref="CancellationToken"/>。</param>
        /// <returns>反序列化后的响应数据。</returns>
        private async Task<T> SendAsync<T>(string url, Dictionary<string, object> p,
            JsonTypeInfo<T>? ti, CancellationToken t)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, new Uri(url));
            req.Headers.Accept.ParseAdd("application/json");
            var json = JsonSerializer.Serialize(p, HttpParameterJsonContext.Default.DictionaryStringObject);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            await _auth.ApplyAuthAsync(req, t).ConfigureAwait(false);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(t);
            cts.CancelAfter(_timeout);
            var r = await _sharedClient.SendAsync(req, cts.Token).ConfigureAwait(false);
            r.EnsureSuccessStatusCode();
            var rj = await r.Content.ReadAsStringAsync().ConfigureAwait(false);
            return ti == null ? JsonSerializer.Deserialize<T>(rj) : JsonSerializer.Deserialize(rj, ti);
        }

        private static bool IsRetryable(Exception ex)
        {
            if (ex is OperationCanceledException) return false;
            if (ex is TaskCanceledException or TimeoutException or System.IO.IOException) return true;
            if (ex is HttpRequestException h && (h.Message ?? "").Contains("timeout", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
