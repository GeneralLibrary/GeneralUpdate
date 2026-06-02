using System.Diagnostics;
using GeneralUpdate.Bowl;
using GeneralUpdate.Bowl.Internal;
using GeneralUpdate.Bowl.Strategies;

namespace BowlTest.Utilities;

/// <summary>
/// Test fakes for internal types that Moq cannot proxy.
/// </summary>

internal class FakeBowlStrategy : IBowlStrategy
{
    public ProcessStartInfo? PrepareResult { get; set; }
    public bool PrepareCalled { get; private set; }
    public bool PostProcessCalled { get; private set; }
    public Exception? PostProcessException { get; set; }
    public Exception? PrepareException { get; set; }

    public ProcessStartInfo? Prepare(in BowlContext context)
    {
        PrepareCalled = true;
        if (PrepareException != null) throw PrepareException;
        return PrepareResult;
    }

    public Task PostProcessAsync(in BowlContext context, ProcessExitResult exitResult, CancellationToken ct)
    {
        PostProcessCalled = true;
        if (PostProcessException != null) throw PostProcessException;
        return Task.CompletedTask;
    }
}

internal class FakeCrashReporter : ICrashReporter
{
    public string? ReportPath { get; set; }
    public bool GenerateReportCalled { get; private set; }
    public Exception? GenerateReportException { get; set; }

    public Task<string> GenerateReportAsync(
        BowlContext context,
        IReadOnlyList<string> outputLines,
        CancellationToken ct)
    {
        GenerateReportCalled = true;
        if (GenerateReportException != null) throw GenerateReportException;
        return Task.FromResult(ReportPath ?? "/tmp/report.json");
    }
}

internal class FakeSystemInfoProvider : ISystemInfoProvider
{
    public bool ExportCalled { get; private set; }
    public Exception? ExportException { get; set; }

    public Task ExportAsync(string outputDirectory, CancellationToken ct)
    {
        ExportCalled = true;
        if (ExportException != null) throw ExportException;
        return Task.CompletedTask;
    }
}
