using System.Collections.Generic;
using System.IO;

namespace GeneralUpdate.Core.Differential;

/// <summary>
/// 定义补丁应用（Dirty）阶段的匹配逻辑接口，用于查找与现有应用程序文件对应的差异补丁文件。
/// </summary>
/// <remarks>
/// <para>
/// Dirty 阶段（补丁应用阶段）是差异更新的第二步，它与 <see cref="ICleanMatcher"/>
/// 定义的 Clean 阶段（差异生成阶段）相对应：
/// </para>
/// <list type="bullet">
///   <item><description><b>Clean 阶段</b>：在服务端/构建时执行，分析新旧版本差异并生成补丁包。</description></item>
///   <item><description><b>Dirty 阶段</b>：在客户端/运行时执行，接收补丁包并应用到现有文件上。</description></item>
/// </list>
/// <para>
/// DirtyMatcher 负责将补丁目录中的 <c>.patch</c> 文件与客户端现有的应用程序文件建立对应关系。
/// 匹配成功后将由差异引擎（如 <c>PatchService</c>）将补丁应用到旧文件上，生成更新后的文件。
/// </para>
/// <para>
/// 默认实现请参考 <see cref="DefaultDirtyMatcher"/>，它通过文件名匹配规则查找补丁文件。
/// </para>
/// </remarks>
public interface IDirtyMatcher
{
    /// <summary>
    /// 从可用的补丁文件集合中查找与指定应用程序文件对应的补丁文件。
    /// </summary>
    /// <param name="oldFile">需要被补丁的现有应用程序文件。</param>
    /// <param name="patchFiles">补丁目录中所有可用文件的集合。</param>
    /// <returns>
    /// 匹配到的补丁 <see cref="FileInfo"/>；如果未找到对应的补丁文件则返回 <c>null</c>
    /// （表示该文件不需要更新或应直接替换）。
    /// </returns>
    /// <remarks>
    /// 实现应基于文件名、相对路径或其他标识信息建立匹配关系。
    /// 返回 <c>null</c> 时，调用方应直接复制新版本文件替换旧版本文件。
    /// </remarks>
    FileInfo? Match(FileInfo oldFile, IEnumerable<FileInfo> patchFiles);
}
