using System;
using System.Collections.Generic;

namespace GeneralUpdate.Core.Download.Reporting;

/// <summary>
///     Maps the local attempt-stage vocabulary onto the server-facing failure phases
///     (<see cref="UpdatePhases"/> — the GeneralSpace ReportDTO vocabulary). Two source
///     vocabularies feed it: the attempt lifecycle stages (<c>downloading</c>,
///     <c>validating</c>, <c>filesApplying</c>, …) and the pipeline middleware class names
///     the pipeline builder stamps into <c>ex.Data["UpdateStage"]</c>.
/// </summary>
public static class UpdatePhaseMapper
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // Attempt lifecycle stages.
        ["downloading"] = UpdatePhases.Download,
        ["downloaded"] = UpdatePhases.Download,
        ["validating"] = UpdatePhases.Verify,
        ["decompressing"] = UpdatePhases.Extract,
        ["backup"] = UpdatePhases.Apply,
        ["manifest"] = UpdatePhases.Apply,
        ["filesApplying"] = UpdatePhases.Apply,
        ["filesApplied"] = UpdatePhases.Apply,
        ["launching"] = UpdatePhases.Startup,
        ["launchingUpdater"] = UpdatePhases.Startup,
        ["awaitingHealth"] = UpdatePhases.Startup,

        // Pipeline middleware class names (PipelineBuilder failure tagging).
        ["HashMiddleware"] = UpdatePhases.Verify,
        ["CompressMiddleware"] = UpdatePhases.Extract,
        ["PatchMiddleware"] = UpdatePhases.Apply,
    };

    /// <summary>
    ///     Resolves the server-facing phase for a local stage marker; <c>null</c> when the
    ///     stage has no unambiguous server meaning (preparing, handedOff, failed, …) — the
    ///     server then classifies the failure as Unknown instead of guessing.
    /// </summary>
    public static string? FromStage(string? stage)
        => stage != null && Map.TryGetValue(stage, out var phase) ? phase : null;
}
