using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core;

namespace GeneralUpdate.Bowl.Internal;

/// <summary>
/// Default crash reporter that serializes a <see cref="Crash"/> record to JSON.
/// Replaces the inline <c>CreateCrash()</c> method in the old WindowStrategy.
/// </summary>
internal sealed class CrashReporter : ICrashReporter
{
    public Task<string> GenerateReportAsync(
        BowlContext context,
        IReadOnlyList<string> outputLines,
        CancellationToken ct)
    {
        GeneralTracer.Info("CrashReporter.GenerateReportAsync: serializing crash report.");

        var crash = new Crash
        {
            Parameter = new Strategys.MonitorParameter
            {
                TargetPath = context.TargetPath,
                FailDirectory = context.FailDirectory,
                BackupDirectory = context.BackupDirectory,
                ProcessNameOrId = context.ProcessNameOrId,
                DumpFileName = context.DumpFileName,
                FailFileName = context.FailFileName,
                WorkModel = context.WorkModel,
                ExtendedField = context.ExtendedField,
            },
            ProcdumpOutPutLines = new List<string>(outputLines),
        };

        var failJsonPath = Path.Combine(context.FailDirectory, context.FailFileName);
        StorageManager.CreateJson(failJsonPath, crash, CrashJsonContext.Default.Crash);

        GeneralTracer.Info($"CrashReporter.GenerateReportAsync: report written to {failJsonPath}.");
        return Task.FromResult(failJsonPath);
    }
}
