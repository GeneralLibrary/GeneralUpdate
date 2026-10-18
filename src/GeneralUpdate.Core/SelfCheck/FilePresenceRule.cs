using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.SelfCheck;

/// <summary>
///     Auxiliary self-check rule: critical files exist and are non-empty.
/// </summary>
/// <remarks>
///     <para>
///         Catches the "pipeline extracted something but the payload is broken" family of failures
///         before any process is started (e.g. a zero-byte executable produced by a failed patch).
///         Paths are relative to <see cref="SelfCheckContext.InstallPath"/>.
///     </para>
///     <para>
///         The updater seeds the rule with <c>MainAppName</c>; hosts may register additional
///         instances with their own critical-file manifests via <c>SelfCheckRule&lt;T&gt;()</c>
///         (an empty manifest passes vacuously so a misconfigured registration cannot break the session).
///     </para>
/// </remarks>
public sealed class FilePresenceRule : ISelfCheckRule
{
    private readonly string[] _criticalFiles;

    /// <inheritdoc />
    public string Name => "FilePresence";

    /// <param name="criticalFiles">Paths relative to the install root, e.g. "MyApp.exe", "config/appsettings.json".</param>
    public FilePresenceRule(params string[] criticalFiles)
    {
        _criticalFiles = criticalFiles ?? System.Array.Empty<string>();
    }

    /// <inheritdoc />
    public Task<SelfCheckRuleResult> CheckAsync(SelfCheckContext ctx, CancellationToken ct)
    {
        foreach (var relative in _criticalFiles.Where(f => !string.IsNullOrWhiteSpace(f)))
        {
            var fullPath = Path.Combine(ctx.InstallPath ?? string.Empty, relative);
            if (!File.Exists(fullPath))
                return Task.FromResult(Fail($"critical file missing: {relative}"));

            if (new FileInfo(fullPath).Length == 0)
                return Task.FromResult(Fail($"critical file is empty: {relative}"));
        }

        var detail = _criticalFiles.Length == 0
            ? "no critical files configured"
            : $"all {_criticalFiles.Length} critical file(s) present";
        return Task.FromResult(new SelfCheckRuleResult(Name, true, detail));
    }

    private static SelfCheckRuleResult Fail(string detail) =>
        new(nameof(FilePresenceRule), false, detail);
}
