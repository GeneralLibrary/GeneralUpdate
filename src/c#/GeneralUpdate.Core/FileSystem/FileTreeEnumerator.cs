using System;
using System.Collections.Generic;
using System.IO;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.FileSystem;

/// <summary>
/// 递归枚举指定目录中的所有文件，同时通过 <see cref="IBlackListMatcher"/> 应用黑名单过滤。
/// </summary>
/// <remarks>
/// <para>
/// FileTreeEnumerator 是一个轻量级的文件遍历器，核心流程为：
/// </para>
/// <list type="number">
///   <item><description>从根目录开始逐层递归遍历。</description></item>
///   <item><description>对每个文件，调用 <c>IBlackListMatcher.IsBlacklisted</c> 判断是否跳过。</description></item>
///   <item><description>对每个子目录，调用 <c>IBlackListMatcher.ShouldSkipDirectory</c> 判断是否进入。</description></item>
/// </list>
/// <para>
/// 此遍历器常用于创建 <see cref="FileTreeSnapshot"/> 的输入源。
/// 可通过 <see cref="FromConfig"/> 工厂方法直接从 <see cref="BlackListConfig"/> 创建。
/// </para>
/// </remarks>
public class FileTreeEnumerator
{
    private readonly IBlackListMatcher _matcher;

    /// <summary>
    /// 使用指定的黑名单匹配器初始化 <see cref="FileTreeEnumerator"/> 的新实例。
    /// </summary>
    /// <param name="matcher">黑名单匹配器实例。</param>
    /// <exception cref="ArgumentNullException">当 <paramref name="matcher"/> 为 <c>null</c> 时抛出。</exception>
    public FileTreeEnumerator(IBlackListMatcher matcher)
    {
        _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
    }

    /// <summary>
    /// 枚举根目录下的所有文件，跳过黑名单中的文件和目录。
    /// </summary>
    /// <param name="rootPath">要枚举的根目录路径。</param>
    /// <returns>所有未跳过文件的完整路径集合。</returns>
    /// <remarks>
    /// <para>
    /// 枚举逻辑：
    /// <list type="bullet">
    ///   <item><description>首先枚举根目录中的所有文件，跳过后缀名匹配黑名单的文件。</description></item>
    ///   <item><description>然后枚举根目录中的所有子目录，跳过匹配黑名单的目录。</description></item>
    ///   <item><description>对未跳过的子目录递归执行相同操作。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 此方法使用 <c>yield return</c> 延迟执行，每次只处理一个文件。
    /// </para>
    /// </remarks>
    public IEnumerable<string> EnumerateFiles(string rootPath)
    {
        if (!Directory.Exists(rootPath))
            yield break;

        foreach (var filePath in Directory.EnumerateFiles(rootPath))
        {
            var relativePath = Path.GetFileName(filePath);
            if (!_matcher.IsBlacklisted(relativePath))
                yield return filePath;
        }

        foreach (var dirPath in Directory.EnumerateDirectories(rootPath))
        {
            var dirName = Path.GetFileName(dirPath);
            if (_matcher.ShouldSkipDirectory(dirName))
                continue;

            foreach (var file in EnumerateFiles(dirPath))
                yield return file;
        }
    }

    /// <summary>
    /// 从 <see cref="BlackListConfig"/> 配置快速创建 <see cref="FileTreeEnumerator"/> 实例。
    /// </summary>
    /// <param name="config">黑名单配置对象。</param>
    /// <returns>配置好的 <see cref="FileTreeEnumerator"/> 实例。</returns>
    /// <remarks>
    /// 此工厂方法内部使用 <see cref="DefaultBlackListMatcher"/> 作为黑名单匹配器实现。
    /// </remarks>
    public static FileTreeEnumerator FromConfig(BlackListConfig config)
        => new(new DefaultBlackListMatcher(config));
}
