using System.Collections.Generic;

namespace GeneralUpdate.Core.FileSystem;

/// <summary>
/// 黑名单匹配器接口，定义用于排除特定文件和目录的匹配规则。
/// </summary>
/// <remarks>
/// <para>
/// 此接口是 GeneralUpdate 文件系统过滤层的核心抽象，允许实现者自定义从文件遍历中排除
/// 哪些文件和目录的规则。主要用于：
/// </para>
/// <list type="bullet">
///   <item><description><see cref="StorageManager.ReadFileNode"/> 方法在遍历文件系统时过滤文件。</description></item>
///   <item><description><see cref="FileTreeEnumerator"/> 在枚举文件时应用黑名单规则。</description></item>
///   <item><description>备份和差异更新场景中跳过不需要处理的文件。</description></item>
/// </list>
/// <para>
/// 默认实现请参考 <see cref="DefaultBlackListMatcher"/>。
/// </para>
/// </remarks>
public interface IBlackListMatcher
{
    /// <summary>
    /// 判断指定的文件是否应该被黑名单排除。
    /// </summary>
    /// <param name="relativeFilePath">文件的相对路径或文件名。</param>
    /// <returns>如果文件应被排除则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
    /// <remarks>
    /// 实现通常应提取文件名和扩展名进行匹配检查。此方法在每次遇到文件时都会调用，
    /// 因此实现应保持高效。
    /// </remarks>
    bool IsBlacklisted(string relativeFilePath);

    /// <summary>
    /// 判断指定的文件扩展名是否在黑名单中。
    /// </summary>
    /// <param name="extension">文件扩展名（包含前导点号，如 <c>.log</c>）。</param>
    /// <returns>如果扩展名在黑名单中则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
    /// <remarks>
    /// 此方法专门用于快速检查文件格式/类型级别的黑名单规则。
    /// </remarks>
    bool IsBlacklistedFormat(string extension);

    /// <summary>
    /// 判断指定的目录名是否应该在文件遍历时被跳过。
    /// </summary>
    /// <param name="directoryName">目录名称。</param>
    /// <returns>如果目录应被跳过则返回 <c>true</c>，否则返回 <c>false</c>。</returns>
    /// <remarks>
    /// 当此方法返回 <c>true</c> 时，遍历器将不会进入该目录及其子目录。
    /// </remarks>
    bool ShouldSkipDirectory(string directoryName);
}
