using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GeneralUpdate.Common;

public static class FileExtensions
{
    public static List<FileInfo> AsFileInfo(this List<string> filePaths)
    {
        return filePaths.Select(path => new FileInfo(path)).ToList();
    }
    
    public static List<FileInfo> AsFileInfo(this IReadOnlyList<string> filePaths)
    {
        return filePaths.Select(path => new FileInfo(path)).ToList();
    }
}