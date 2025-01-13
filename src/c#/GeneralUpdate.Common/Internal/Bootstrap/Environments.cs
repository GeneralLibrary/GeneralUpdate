using System.IO;

namespace GeneralUpdate.Common.Internal.Bootstrap;

public static class Environments
{
    public static void SetEnvironmentVariable(string key, string value)
    {
       var filePath = Path.Combine(Path.GetTempPath(), $"{key}.txt");
       File.WriteAllText(filePath, value);
    }

    public static string GetEnvironmentVariable(string key)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{key}.txt");
        if (!File.Exists(filePath))
            return string.Empty;

        var content = File.ReadAllText(filePath);
        File.SetAttributes(filePath, FileAttributes.Normal);
        File.Delete(filePath);
        return content;
    }
}