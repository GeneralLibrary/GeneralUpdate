using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GeneralUpdate.Core.Differential;

/// <summary>
/// <see cref="IDirtyMatcher"/> 的默认实现：通过文件名的智能匹配规则，
/// 在补丁文件集合中查找与应用程序文件对应的差异补丁。
/// </summary>
/// <remarks>
/// <para>
/// 匹配规则：
/// <list type="bullet">
///   <item><description>补丁文件的后缀名必须为 <c>.patch</c>（不区分大小写）。</description></item>
///   <item><description>补丁文件的基名（不含扩展名）需与应用程序文件的名称匹配。</description></item>
///   <item><description>支持嵌套 <c>.patch</c> 后缀的处理：如 <c>example.dll.patch</c>
///   会去掉 <c>.patch</c> 后缀后得到 <c>example.dll</c>，再与应用程序文件名比较。</description></item>
///   <item><description>所有字符串比较不区分大小写。</description></item>
/// </list>
/// </para>
/// <para>
/// 此实现对应的是"脏"（Dirty）阶段，即补丁应用阶段，与 <see cref="DefaultCleanMatcher"/>
/// 的差异生成阶段相对应。
/// </para>
/// </remarks>
public class DefaultDirtyMatcher : IDirtyMatcher
{
    private const string PatchFormat = ".patch";

    /// <inheritdoc/>
    public FileInfo? Match(FileInfo oldFile, IEnumerable<FileInfo> patchFiles)
    {
        var findFile = patchFiles.FirstOrDefault(f =>
        {
            var name = Path.GetFileNameWithoutExtension(f.Name);
            if (name.EndsWith(PatchFormat, System.StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - PatchFormat.Length);
            return name.Equals(oldFile.Name, System.StringComparison.OrdinalIgnoreCase);
        });

        if (findFile != null &&
            Path.GetExtension(findFile.FullName).Equals(PatchFormat, System.StringComparison.OrdinalIgnoreCase))
            return findFile;

        return null;
    }
}
