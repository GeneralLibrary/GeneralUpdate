using System;
using System.Collections.Generic;

namespace GeneralUpdate.Core.Configuration
{
    /// <summary>
    ///     面向外部 API 调用者的更新参数配置类。
    ///     该类专为外部使用者设计，用于配置更新行为所需的核心参数。
    ///     继承自 <see cref="BaseConfigInfo" />，复用公共字段以减少重复并提高可维护性。
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>Configinfo</c> 是更新流程的入口配置对象，由 <see cref="ConfiginfoBuilder" /> 通过建造者模式构建。
    ///         构建完成后，会通过 <see cref="ConfigurationMapper.MapToGlobalConfigInfo" /> 映射为内部运行时配置
    ///         <see cref="GlobalConfigInfo" />，供更新工作流使用。
    ///     </para>
    ///     <para>
    ///         调用 <see cref="Validate" /> 方法可对所有必填字段进行完整性校验，
    ///         确保 <c>UpdateUrl</c>、<c>MainAppName</c>、<c>ClientVersion</c> 等关键参数不为空或格式正确。
    ///     </para>
    /// </remarks>
    /// <seealso cref="BaseConfigInfo" />
    /// <seealso cref="GlobalConfigInfo" />
    /// <seealso cref="ConfiginfoBuilder" />
    /// <seealso cref="ConfigurationMapper" />
    public class Configinfo : BaseConfigInfo
    {
        /// <summary>
        ///     用于检查可用更新的 API 端点 URL。
        ///     客户端通过查询此 URL 来确定是否存在新版本可供更新。
        /// </summary>
        /// <remarks>
        ///     该属性为必填项，在 <see cref="Validate" /> 方法中会校验其是否为有效的绝对 URI 格式。
        ///     如果未配置或格式无效，将抛出 <see cref="ArgumentException" />。
        /// </remarks>
        public string UpdateUrl { get; set; }

        /// <summary>
        ///     升级程序（更新器自身）的当前版本号。
        ///     该版本号用于实现更新器自身的独立升级，与主应用的版本管理解耦。
        /// </summary>
        /// <remarks>
        ///     通过比较 <c>UpgradeClientVersion</c> 与服务端返回的最新版本号，
        ///     可确定是否需要先对更新器本身执行升级操作。
        /// </remarks>
        public string UpgradeClientVersion { get; set; }

        /// <summary>
        ///     用于跟踪和更新管理的唯一产品标识符。
        ///     多个产品可以共享同一套更新基础设施，通过不同的产品 ID 进行区分。
        /// </summary>
        public string ProductId { get; set; }

        /// <summary>
        ///     校验当前配置对象的必填字段是否完整且格式正确。
        /// </summary>
        /// <remarks>
        ///     <para>该方法会对以下字段执行校验逻辑：</para>
        ///     <list type="bullet">
        ///         <item>
        ///             <c>UpdateUrl</c>：不能为空，且必须是有效的绝对 URI。</item>
        ///         <item>
        ///             <c>UpdateLogUrl</c>：如果已设置，则必须是有效的绝对 URI。</item>
        ///         <item>
        ///             <c>UpdateAppName</c>：不能为空。</item>
        ///         <item>
        ///             <c>MainAppName</c>：不能为空。</item>
        ///         <item>
        ///             <c>AppSecretKey</c>：不能为空。</item>
        ///         <item>
        ///             <c>ClientVersion</c>：不能为空。</item>
        ///         <item>
        ///             <c>InstallPath</c>：不能为空。</item>
        ///     </list>
        ///     <para>
        ///         该方法通常在 <see cref="ConfiginfoBuilder.Build" /> 方法的末尾被调用，
        ///         以确保构建出的配置对象是完整且合法的。
        ///     </para>
        /// </remarks>
        /// <exception cref="ArgumentException">
        ///     当任一必填字段为空、仅含空白字符或格式无效时抛出，
        ///     异常消息会指明具体是哪个字段校验失败。
        /// </exception>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(UpdateUrl) || !Uri.IsWellFormedUriString(UpdateUrl, UriKind.Absolute))
                throw new ArgumentException("Invalid UpdateUrl");

            if (!string.IsNullOrWhiteSpace(UpdateLogUrl) && !Uri.IsWellFormedUriString(UpdateLogUrl, UriKind.Absolute))
                throw new ArgumentException("Invalid UpdateLogUrl");

            if (string.IsNullOrWhiteSpace(UpdateAppName))
                throw new ArgumentException("UpdateAppName cannot be empty");

            if (string.IsNullOrWhiteSpace(MainAppName))
                throw new ArgumentException("MainAppName cannot be empty");

            if (string.IsNullOrWhiteSpace(AppSecretKey))
                throw new ArgumentException("AppSecretKey cannot be empty");

            if (string.IsNullOrWhiteSpace(ClientVersion))
                throw new ArgumentException("ClientVersion cannot be empty");

            if (string.IsNullOrWhiteSpace(InstallPath))
                throw new ArgumentException("InstallPath cannot be empty");
        }
    }
}
