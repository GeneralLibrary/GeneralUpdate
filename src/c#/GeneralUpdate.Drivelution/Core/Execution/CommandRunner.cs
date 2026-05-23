using System.Diagnostics;
using System.Text;
using GeneralUpdate.Common.Shared;

namespace GeneralUpdate.Drivelution.Core.Execution;

/// <summary>
/// Default implementation of <see cref="ICommandRunner"/>.
/// Uses <see cref="ProcessStartInfo.ArgumentList"/> for safe argument passing—no shell parsing,
/// no injection risk from paths with spaces or special characters.
/// </summary>
public class CommandRunner : ICommandRunner
{
    /// <inheritdoc/>
    public async Task<CommandResult> RunAsync(
        string command,
        string[] arguments,
        CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Safe: each argument added individually, never parsed by a shell
        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

        GeneralTracer.Debug($"Running: {command} {string.Join(" ", arguments)}");

        using var process = new Process { StartInfo = psi };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                stderr.AppendLine(e.Data);
        };

        if (!process.Start())
            throw new InvalidOperationException($"Failed to start process: {command}");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        return new CommandResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = stdout.ToString(),
            StandardError = stderr.ToString()
        };
    }

    /// <inheritdoc/>
    public async Task<CommandResult> RunOrThrowAsync(
        string command,
        string[] arguments,
        CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(command, arguments, cancellationToken);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Command '{command}' failed with exit code {result.ExitCode}. " +
                $"Error: {result.StandardError.Trim()}");
        }

        return result;
    }
}
