using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using GeneralUpdate.Core.Download.Reporting;

namespace GeneralUpdate.Core.Configuration;

/// <summary>Stable random identity stored outside the installation and backup trees.</summary>
internal static class InstallationIdentity
{
    internal static string GetOrCreate(UpdateContext config)
    {
        var root = Path.GetFullPath(config.DiagnosticsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GeneralUpdate", "state"));
        UpdateAttempt.ValidateExternalPath(root, config);
        var install = Path.GetFullPath(config.InstallPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (Path.DirectorySeparatorChar == '\\') install = install.ToUpperInvariant();
        using var sha = SHA256.Create();
        var hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(install))).Replace("-", "").ToLowerInvariant();
        var directory = Path.Combine(root, "installations");
        UpdateAttempt.ValidateExternalPath(directory, config);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, hash + ".id");
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The installation identity cannot be a symbolic link.");
        if (!File.Exists(path))
        {
            var temp = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                var bytes = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString("D"));
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                try { File.Move(temp, path); }
                catch (IOException) when (File.Exists(path)) { }
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("The installation identity cannot be a symbolic link.");
        var value = File.ReadAllText(path);
        if (!Guid.TryParseExact(value, "D", out var id) || id == Guid.Empty)
            throw new InvalidDataException("The installation identity is invalid.");
        return id.ToString("D");
    }
}
