using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using GeneralUpdate.Core.JsonContext;

namespace GeneralUpdate.Core.SelfCheck;

/// <summary>
///     Well-known states of <see cref="UpdateVerificationMarkerInfo.State"/>.
/// </summary>
public static class MarkerStates
{
    /// <summary>Updater started the target application and the observation window is running.</summary>
    public const string Launched = "Launched";
}

/// <summary>
///     DTO for <c>generalupdate.verify.json</c> — the verify marker the updater writes before
///     starting the freshly updated main application.
/// </summary>
/// <remarks>
///     <para>
///         Lifecycle (owned exclusively by the updater): written as <see cref="MarkerStates.Launched"/>
///         before the launch → deleted after the verdict (pass) or alongside the rollback (fail).
///         A marker that survives into the next client start is an "orphan" — the updater died before
///         rendering a verdict — and drives the client-side fallback recovery (rollback within the
///         window period, cleanup-only beyond it).
///     </para>
///     <para>
///         Serialized through <see cref="UpdateVerificationMarkerJsonContext"/> (source-gen) to stay
///         Native AOT compatible.
///     </para>
/// </remarks>
public class UpdateVerificationMarkerInfo
{
    /// <summary>Marker contract version. Readers skip/repair markers with an unknown schema.</summary>
    [JsonPropertyName("schema")]
    public int Schema { get; set; } = UpdateVerificationMarker.CurrentSchema;

    /// <summary>The version that was just applied and is under verification.</summary>
    [JsonPropertyName("targetVersion")]
    public string? TargetVersion { get; set; }

    /// <summary>The version to roll back to (the version before this update).</summary>
    [JsonPropertyName("previousVersion")]
    public string? PreviousVersion { get; set; }

    /// <summary>Backup directory recorded for this update; rollback material.</summary>
    [JsonPropertyName("backupDirectory")]
    public string? BackupDirectory { get; set; }

    /// <summary>UTC timestamp of marker creation; drives the orphan age decision.</summary>
    [JsonPropertyName("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Marker state; see <see cref="MarkerStates"/>.</summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = MarkerStates.Launched;
}

/// <summary>
///     Read/write/delete access to the verify marker plus the verify-ok / verify-fail
///     acknowledgement files. All path conventions of the self-check feature live here:
///     the marker and the ack files are siblings inside the install root.
/// </summary>
/// <remarks>
///     <para>
///         The updater is the only writer of the marker; the restarted application is the only
///         writer of the ack files (via <see cref="UpdateSelfCheck.ReportHealth"/>). Corrupted or
///         unknown-schema markers are tolerated (read as absent) so a truncated file can never
///         wedge the update pipeline.
///     </para>
/// </remarks>
public static class UpdateVerificationMarker
{
    /// <summary>Marker file name; lives in the install root next to <c>generalupdate.manifest.json</c>.</summary>
    public const string FileName = "generalupdate.verify.json";

    /// <summary>Acknowledgement file written by the application when business health checks pass.</summary>
    public const string AckOkFileName = "generalupdate.verify.ok";

    /// <summary>Acknowledgement file written by the application when business health checks fail.</summary>
    public const string AckFailFileName = "generalupdate.verify.fail";

    /// <summary>Current marker contract version.</summary>
    public const int CurrentSchema = 1;

    /// <summary>
    ///     Orphan window: markers older than this are cleaned up only — never rolled back —
    ///     because the observation window they belonged to is long gone and a rollback would
    ///     downgrade the installation without evidence.
    /// </summary>
    public static readonly TimeSpan OrphanWindow = TimeSpan.FromHours(24);

    /// <summary>Full path of the marker file inside <paramref name="installPath"/>.</summary>
    public static string GetPath(string installPath) => Path.Combine(installPath ?? string.Empty, FileName);

    /// <summary>
    ///     Writes a <see cref="MarkerStates.Launched"/> marker. Call immediately before starting
    ///     the updated application so the client-side orphan detection has full rollback material
    ///     if the updater dies inside the observation window.
    /// </summary>
    public static void Write(
        string installPath,
        string? targetVersion,
        string? previousVersion,
        string? backupDirectory)
    {
        var marker = new UpdateVerificationMarkerInfo
        {
            TargetVersion = targetVersion,
            PreviousVersion = previousVersion,
            BackupDirectory = backupDirectory,
            CreatedAtUtc = DateTime.UtcNow,
            State = MarkerStates.Launched
        };
        var json = JsonSerializer.Serialize(marker, UpdateVerificationMarkerJsonContext.Default.UpdateVerificationMarkerInfo);
        File.WriteAllText(GetPath(installPath), json);
        GeneralTracer.Info($"UpdateVerificationMarker: written for target {targetVersion} (previous {previousVersion}).");
    }

    /// <summary>
    ///     Reads the marker; returns <c>null</c> when absent, corrupted, or of an unknown schema.
    /// </summary>
    public static UpdateVerificationMarkerInfo? TryRead(string installPath)
    {
        var path = GetPath(installPath);
        if (string.IsNullOrWhiteSpace(installPath) || !File.Exists(path))
            return null;

        try
        {
            var marker = JsonSerializer.Deserialize(
                File.ReadAllText(path), UpdateVerificationMarkerJsonContext.Default.UpdateVerificationMarkerInfo);

            if (marker == null || marker.Schema != CurrentSchema)
            {
                GeneralTracer.Warn($"UpdateVerificationMarker: ignoring marker with schema {marker?.Schema.ToString() ?? "null"} at {path}.");
                return null;
            }

            return marker;
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"UpdateVerificationMarker: corrupted marker at {path}: {ex.Message}");
            return null;
        }
    }

    /// <summary>Deletes the marker. Returns <c>true</c> when it is gone afterwards (or never existed).</summary>
    public static bool Delete(string installPath)
    {
        try
        {
            var path = GetPath(installPath);
            if (!File.Exists(path)) return true;
            File.Delete(path);
            return true;
        }
        catch (Exception ex)
        {
            GeneralTracer.Warn($"UpdateVerificationMarker: failed to delete marker: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    ///     Orphan age decision: <c>true</c> when the marker outlived <see cref="OrphanWindow"/>
    ///     (cleanup-only, no rollback).
    /// </summary>
    public static bool IsOrphanedBeyondWindow(UpdateVerificationMarkerInfo marker)
    {
        if (marker == null) return false;
        return DateTime.UtcNow - marker.CreatedAtUtc > OrphanWindow;
    }

    /// <summary>
    ///     Reads the acknowledgement state written by the application
    ///     (verify-fail wins over verify-ok so a late failure is never masked).
    /// </summary>
    /// <param name="installPath">Install root containing the ack files.</param>
    /// <param name="reason">Bounded failure reason when the state is <see cref="VerificationAckState.Fail"/>.</param>
    public static VerificationAckState TryReadAck(string installPath, out string? reason)
    {
        reason = null;
        if (string.IsNullOrWhiteSpace(installPath))
            return VerificationAckState.None;

        var failPath = Path.Combine(installPath, AckFailFileName);
        if (File.Exists(failPath))
        {
            reason = ReadBounded(failPath);
            return VerificationAckState.Fail;
        }

        if (File.Exists(Path.Combine(installPath, AckOkFileName)))
            return VerificationAckState.Ok;

        return VerificationAckState.None;
    }

    /// <summary>Writes the acknowledgement file on behalf of the application (see <see cref="UpdateSelfCheck.ReportHealth"/>).</summary>
    public static void WriteAck(string installPath, bool healthy, string? reason)
    {
        var path = Path.Combine(installPath ?? string.Empty, healthy ? AckOkFileName : AckFailFileName);
        File.WriteAllText(path, reason ?? (healthy ? "healthy" : "unhealthy"));
        GeneralTracer.Info($"UpdateVerificationMarker: ack written ({(healthy ? "ok" : "fail")}).");
    }

    /// <summary>
    ///     Removes leftover ack files. Callers must clear them BEFORE relaunching the application:
    ///     a stale verify-ok from a previous session would otherwise satisfy the next strict check.
    /// </summary>
    public static void DeleteAckFiles(string installPath)
    {
        foreach (var name in new[] { AckOkFileName, AckFailFileName })
        {
            try
            {
                var path = Path.Combine(installPath ?? string.Empty, name);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                GeneralTracer.Warn($"UpdateVerificationMarker: failed to delete ack file {name}: {ex.Message}");
            }
        }
    }

    private static string ReadBounded(string path)
    {
        try
        {
            var text = File.ReadAllText(path)?.Trim();
            if (string.IsNullOrEmpty(text)) return "unknown reason";
            return text.Length <= 512 ? text : text.Substring(0, 512);
        }
        catch
        {
            return "unreadable fail file";
        }
    }
}

/// <summary>Acknowledgement state of the file-based health protocol.</summary>
public enum VerificationAckState
{
    /// <summary>No acknowledgement file present (yet).</summary>
    None,

    /// <summary>Application reported healthy (verify-ok).</summary>
    Ok,

    /// <summary>Application reported unhealthy (verify-fail).</summary>
    Fail
}

/// <summary>
///     Host-facing convenience API for the restarted main application (client side of the
///     self-check cooperation protocol). A no-op for applications that do not integrate —
///     the default lenient policy passes on process liveness alone.
/// </summary>
public static class UpdateSelfCheck
{
    /// <summary>
    ///     Returns the pending verification context when the process was (re)started by the
    ///     updater and its verdict is still open, otherwise <c>null</c>. Call during startup,
    ///     before running business health checks.
    /// </summary>
    public static PendingVerification? GetPendingVerification(string installPath)
    {
        var marker = UpdateVerificationMarker.TryRead(installPath);
        if (marker == null)
            return null;

        return new PendingVerification(
            marker.TargetVersion,
            marker.PreviousVersion,
            UpdateVerificationMarker.GetPath(installPath),
            marker.BackupDirectory);
    }

    /// <summary>
    ///     Reports the business self-check outcome (writes verify-ok / verify-fail).
    ///     In strict mode (<c>SelfCheckRequireAck</c>) the updater rolls the application back
    ///     when the outcome is unhealthy.
    /// </summary>
    public static void ReportHealth(string installPath, bool healthy, string? reason = null)
        => UpdateVerificationMarker.WriteAck(installPath, healthy, reason);
}

/// <summary>Pending verification as seen by the restarted application.</summary>
/// <param name="TargetVersion">Version under verification.</param>
/// <param name="PreviousVersion">Version the updater will roll back to on failure.</param>
/// <param name="MarkerPath">Full path of the verify marker file.</param>
/// <param name="BackupDirectory">Backup directory recorded by the updater (rollback material).</param>
public sealed record PendingVerification(
    string? TargetVersion,
    string? PreviousVersion,
    string MarkerPath,
    string? BackupDirectory);
