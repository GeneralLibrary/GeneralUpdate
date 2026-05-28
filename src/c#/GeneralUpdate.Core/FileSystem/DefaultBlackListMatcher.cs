using System;
using System.IO;
using System.Linq;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.FileSystem;

/// <summary>
/// 基于 Glob 模式的黑名单匹配器默认实现，由 <see cref="BlackListConfig"/> 驱动。
/// </summary>
/// <remarks>
/// <para>
/// DefaultBlackListMatcher 实现了 <see cref="IBlackListMatcher"/> 接口，提供三种类型的黑名单过滤：
/// </para>
/// <list type="bullet">
///   <item><description><b>黑名单文件</b>：按照 Glob 模式匹配合文件名（如 <c>*.log</c> 匹配所有日志文件）。</description></item>
///   <item><description><b>黑名单格式</b>：按照文件扩展名进行不区分大小写的精确匹配。</description></item>
///   <item><description><b>跳过目录</b>：按照子字符串包含匹配（不区分大小写）判断是否跳过子目录。</description></item>
/// </list>
/// <para>
/// 此类的实例通常通过 <see cref="FromConfigInfo"/> 工厂方法从 <see cref="GlobalConfigInfo"/> 创建，
/// 或通过构造函数直接传入 <see cref="BlackListConfig"/> 配置对象。
/// </para>
/// </remarks>
public class DefaultBlackListMatcher : IBlackListMatcher
{
    private readonly BlackListConfig _config;

    /// <summary>
    /// 使用指定的黑名单配置初始化 <see cref="DefaultBlackListMatcher"/> 的新实例。
    /// </summary>
    /// <param name="config">黑名单配置对象，包含要排除的文件名模式、扩展名和目录名。</param>
    /// <exception cref="ArgumentNullException">当 <paramref name="config"/> 为 <c>null</c> 时抛出。</exception>
    public DefaultBlackListMatcher(BlackListConfig config)
        => _config = config ?? throw new ArgumentNullException(nameof(config));

    /// <summary>
    /// 从 <see cref="GlobalConfigInfo"/> 中的黑名单属性创建匹配器实例。
    /// </summary>
    /// <param name="config">全局配置信息对象。</param>
    /// <returns>配置好的 <see cref="DefaultBlackListMatcher"/> 实例。</returns>
    /// <remarks>
    /// 此工厂方法只会在相应列表有元素（<c>Count &gt; 0</c>）时才会设置对应的黑名单规则，
    /// 空列表会被视为 <c>null</c>（即不启用该类型的过滤）。
    /// </remarks>
    public static DefaultBlackListMatcher FromConfigInfo(GlobalConfigInfo config)
    {
        var cfg = new BlackListConfig(
            config.BlackFiles?.Count > 0 ? config.BlackFiles : null,
            config.BlackFormats?.Count > 0 ? config.BlackFormats : null,
            config.SkipDirectorys?.Count > 0 ? config.SkipDirectorys : null);
        return new DefaultBlackListMatcher(cfg);
    }

    /// <summary>
    /// 判断指定文件是否被黑名单匹配规则排除。
    /// </summary>
    /// <param name="relativeFilePath">文件的相对路径或文件名。</param>
    /// <returns>如果文件匹配黑名单规则则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
    /// <remarks>
    /// 匹配逻辑依次检查：
    /// <list type="number">
    ///   <item><description>文件名是否匹配 <c>BlackFiles</c> 中的任一 Glob 模式。</description></item>
    ///   <item><description>文件扩展名是否匹配 <c>BlackFormats</c> 中的任一格式。</description></item>
    /// </list>
    /// 只要满足任一条件即视为黑名单文件。
    /// </remarks>
    public bool IsBlacklisted(string relativeFilePath)
    {
        var fileName = Path.GetFileName(relativeFilePath);
        var ext = Path.GetExtension(relativeFilePath);

        if (_config.BlackFiles?.Any(f => MatchGlob(fileName, f)) == true) return true;
        if (_config.BlackFormats?.Any(f => string.Equals(f, ext, StringComparison.OrdinalIgnoreCase)) == true) return true;
        return false;
    }

    /// <summary>
    /// 判断指定的文件扩展名是否在黑名单格式列表中。
    /// </summary>
    /// <param name="extension">文件扩展名（如 <c>.log</c>、<c>.tmp</c>）。</param>
    /// <returns>如果扩展名在黑名单中则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
    /// <remarks>
    /// 比较时使用不区分大小写的字符串比较。
    /// </remarks>
    public bool IsBlacklistedFormat(string extension)
        => _config.BlackFormats?.Any(f => string.Equals(f, extension, StringComparison.OrdinalIgnoreCase)) == true;

    /// <summary>
    /// 判断指定的目录名是否应该被跳过（即不进入该目录进行遍历）。
    /// </summary>
    /// <param name="directoryName">目录名称。</param>
    /// <returns>如果目录名匹配任一跳过规则则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
    /// <remarks>
    /// 使用不区分大小写的子字符串包含匹配（<c>string.IndexOf</c>）进行判断。
    /// 只要目录名中包含 <c>SkipDirectorys</c> 列表中的任一字符串，即判定为应该跳过。
    /// </remarks>
    public bool ShouldSkipDirectory(string directoryName)
        => _config.SkipDirectorys?.Any(d =>
            directoryName.IndexOf(d, StringComparison.OrdinalIgnoreCase) >= 0) == true;

    /// <summary>
    /// 使用简单的 Glob 模式匹配文件名。
    /// </summary>
    /// <param name="input">要匹配的文件名。</param>
    /// <param name="pattern">Glob 模式（支持 <c>*.xxx</c> 通配前缀匹配和精确匹配）。</param>
    /// <returns>如果文件名匹配模式则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
    /// <remarks>
    /// 目前支持两种 Glob 模式：
    /// <list type="bullet">
    ///   <item><description><c>*.log</c>：以 <c>.log</c> 结尾的通配匹配。</description></item>
    ///   <item><description><c>filename</c>：不区分大小写的精确文件名匹配。</description></item>
    /// </list>
    /// </remarks>
    private static bool MatchGlob(string input, string pattern)
    {
        if (pattern.StartsWith("*."))
        {
            var ext = pattern.Substring(1);
            return input.EndsWith(ext, StringComparison.OrdinalIgnoreCase);
        }
        return string.Equals(input, pattern, StringComparison.OrdinalIgnoreCase);
    }
}
