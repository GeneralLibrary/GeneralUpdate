using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core.JsonContext;

namespace GeneralUpdate.Core.Diagnostics;

/// <summary>
///     Packages diagnostics into a zip archive under the configured size cap.
/// </summary>
/// <remarks>
///     <para>
///         Two packing modes share one implementation:
///     </para>
///     <list type="bullet">
///         <item>real-time bundles (collector output) are packed from a staging directory;</item>
///         <item>outbox evidence (Bowl crash directories) is packed from the evidence directory as-is.</item>
///     </list>
///     <para>
///         Oversized archives are repacked with dump files (<c>*.dmp</c>) stripped — the text
///         evidence (crash report, system info, manifest) still reaches the server. When even the
///         text-only archive exceeds the cap, packing fails and the caller keeps the evidence on
///         disk (never deletes diagnostics it could not deliver).
///     </para>
///     <para>
///         Files matching the configured blacklist (<see cref="FileSystem.StorageManager.BlackMatcher"/>,
///         same idea as the backup/update filters) never leave the machine — that is the privacy gate.
///     </para>
/// </remarks>
internal static class DiagnosticsPackager
{
    private const string DumpExtension = ".dmp";
    private const long LogTailBytes = 64 * 1024;

    /// <summary>Packs a directory. Returns the archive path and the list of included file names.</summary>
    /// <param name="sourceDirectory">Directory whose files become archive entries (recursive).</param>
    /// <param name="options">Runtime options (size cap).</param>
    /// <param name="privacyFilter">
    ///     Optional blacklist filter — files it marks never leave the machine. Injected
    ///     explicitly (the ambient <c>StorageManager.BlackMatcher</c> static is wired in by the
    ///     bootstrap, never read at pack time) so packing is deterministic under parallel state.
    /// </param>
    /// <returns>Archive path + included entry names, or <c>null</c> when nothing packable fits under the cap.</returns>
    public static (string ZipPath, List<string> Entries)? PackDirectory(
        string sourceDirectory, DiagnosticsOptions options, IBlackMatcher? privacyFilter = null)
    {
        if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
            return null;

        var allFiles = EnumerateFiles(sourceDirectory).ToList();
        if (allFiles.Count == 0)
            return null;

        var zipPath = Path.Combine(Path.GetTempPath(), $"generalupdate-diagnostics-{Guid.NewGuid():N}.zip");
        var included = WriteArchive(zipPath, sourceDirectory, allFiles, privacyFilter, stripDumps: false);

        // Over the cap → strip dump files and try once more; they are by far the usual offenders.
        if (included.Count > 0 && new FileInfo(zipPath).Length > options.MaxSizeBytes && included.Any(IsDump))
        {
            File.Delete(zipPath);
            included = WriteArchive(zipPath, sourceDirectory,
                allFiles.Where(f => !IsDump(f)).ToList(), privacyFilter, stripDumps: true);
        }

        if (included.Count == 0 || new FileInfo(zipPath).Length > options.MaxSizeBytes)
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
            GeneralTracer.Warn("DiagnosticsPackager: bundle exceeds the size cap even without dumps — not delivered.");
            return null;
        }

        return (zipPath, included);
    }

    /// <summary>
    ///     Packs a real-time bundle from in-memory text payloads plus the standard
    ///     install-directory artifacts (manifest snapshot, trace-log tail) into a staging directory,
    ///     then archives it. A schema-2 <c>manifest.json</c> (same disk contract as the Bowl
    ///     outbox) is written into the archive so the server can identify the bundle and treat
    ///     it as parsed evidence — real-time bundles are first-class outbox citizens.
    /// </summary>
    /// <param name="bundleGuid">Canonical bundle id; the server deduplicates uploads on it.</param>
    public static (string ZipPath, List<string> Entries)? PackBundle(
        DiagnosticsCollectRequest request, DiagnosticsOptions options, string bundleGuid,
        IBlackMatcher? privacyFilter = null)
    {
        var stagingRoot = Path.Combine(Path.GetTempPath(), $"generalupdate-diagnostics-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(stagingRoot);

            WriteIfAny(Path.Combine(stagingRoot, "self-check-report.json"), request.SelfCheckReportJson);
            WriteIfAny(Path.Combine(stagingRoot, "rollback-result.json"), request.RollbackResultJson);

            // Manifest snapshot: lets the server see the identity the client was running with.
            var manifestPath = Path.Combine(request.InstallPath ?? string.Empty,
                Configuration.ManifestInfo.FileName);
            if (File.Exists(manifestPath))
                File.Copy(manifestPath, Path.Combine(stagingRoot, Configuration.ManifestInfo.FileName), overwrite: true);

            var logTail = ReadLogTail(request.InstallPath);
            WriteIfAny(Path.Combine(stagingRoot, "update-log-tail.txt"), logTail);

            WriteIfAny(Path.Combine(stagingRoot, "manifest.json"), BuildManifestJson(request, options, bundleGuid));

            return PackDirectory(stagingRoot, options, privacyFilter);
        }
        finally
        {
            TryDeleteDirectory(stagingRoot);
        }
    }

    /// <summary>
    ///     In-archive manifest (schema 2 disk contract): identity + kind + file list. Written
    ///     before packing so <see cref="PackDirectory"/> includes it in the audit list.
    /// </summary>
    private static string BuildManifestJson(
        DiagnosticsCollectRequest request, DiagnosticsOptions options, string bundleGuid)
    {
        var manifest = new OutboxManifest
        {
            Schema = OutboxManifest.SupportedSchema,
            Guid = bundleGuid,
            Kind = request.Kind,
            TargetName = options.TargetName,
            Version = request.TargetVersion,
            CreatedAtUtc = DateTime.UtcNow,
            State = OutboxStates.Pending,
        };
        return JsonSerializer.Serialize(manifest, DiagnosticsJsonContext.Default.OutboxManifest);
    }

    private static List<string> WriteArchive(
        string zipPath, string sourceDirectory, List<string> files,
        IBlackMatcher? privacyFilter, bool stripDumps)
    {
        var included = new List<string>();
        try
        {
            using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            foreach (var file in files)
            {
                if (stripDumps && IsDump(file)) continue;
                if (privacyFilter != null && privacyFilter.IsBlacklisted(file))
                {
                    GeneralTracer.Info($"DiagnosticsPackager: blacklisted file excluded: {Path.GetFileName(file)}.");
                    continue;
                }

                var entryName = file.Substring(sourceDirectory.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace(Path.DirectorySeparatorChar, '/');
                archive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
                included.Add(entryName);
            }
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"DiagnosticsPackager: archive write failed: {ex.Message}");
            try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { /* best effort */ }
            included.Clear();
        }

        return included;
    }

    private static IEnumerable<string> EnumerateFiles(string directory)
    {
        var queue = new Queue<string>();
        queue.Enqueue(directory);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            string[] files = Array.Empty<string>(), subDirectories = Array.Empty<string>();
            try
            {
                files = Directory.GetFiles(current);
                subDirectories = Directory.GetDirectories(current);
            }
            catch (Exception ex)
            {
                GeneralTracer.Warn($"DiagnosticsPackager: cannot enumerate '{current}': {ex.Message}");
            }

            foreach (var file in files)
                yield return file;
            foreach (var sub in subDirectories)
                queue.Enqueue(sub);
        }
    }

    /// <summary>Tail of the newest GeneralUpdate trace log — the "update log" evidence.</summary>
    internal static string? ReadLogTail(string installPath)
    {
        try
        {
            var candidates = new[] { installPath, AppDomain.CurrentDomain.BaseDirectory }
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => Path.Combine(p, "Logs"))
                .Where(Directory.Exists)
                .SelectMany(d => Directory.GetFiles(d, "generalupdate-trace*.log"))
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToList();

            var newest = candidates.FirstOrDefault();
            if (newest == null) return null;

            using var stream = newest.OpenRead();
            var skip = Math.Max(0, stream.Length - LogTailBytes);
            stream.Seek(skip, SeekOrigin.Begin);
            using var reader = new StreamReader(stream);
            var tail = reader.ReadToEnd();
            return skip > 0 ? $"[...truncated to the last {LogTailBytes / 1024} KB...]{Environment.NewLine}{tail}" : tail;
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"DiagnosticsPackager: reading trace log tail failed: {ex.Message}");
            return null;
        }
    }

    private static bool IsDump(string path) =>
        Path.GetExtension(path).Equals(DumpExtension, StringComparison.OrdinalIgnoreCase);

    private static void WriteIfAny(string path, string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;
        File.WriteAllText(path, content);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) StorageManager.DeleteDirectory(path);
        }
        catch
        {
            // Staging cleanup is best-effort; the OS temp cleaner will reap leftovers.
        }
    }
}
