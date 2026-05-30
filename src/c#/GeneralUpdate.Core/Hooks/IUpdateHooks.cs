using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Hooks;

/// <summary>Lifecycle hooks for update processes.</summary>
public interface IUpdateHooks
{
    Task<bool> OnBeforeUpdateAsync(HookContext ctx);
    Task OnDownloadCompletedAsync(DownloadContext ctx);
    Task OnAfterUpdateAsync(HookContext ctx);
    Task OnUpdateErrorAsync(HookContext ctx, Exception ex);
    Task OnBeforeStartAppAsync(HookContext ctx);
}

public record HookContext(
    string UpdateAppName,
    string InstallPath,
    string CurrentVersion,
    string? TargetVersion,
    Configuration.AppType AppType
);

public record DownloadContext(
    string AssetName,
    string Version,
    long TotalBytes,
    TimeSpan Duration,
    string? LocalPath,
    bool Success
);

/// <summary>Default no-op hooks.</summary>
public class NoOpUpdateHooks : IUpdateHooks
{
    public Task<bool> OnBeforeUpdateAsync(HookContext ctx) => Task.FromResult(true);
    public Task OnDownloadCompletedAsync(DownloadContext ctx) => Task.CompletedTask;
    public Task OnAfterUpdateAsync(HookContext ctx) => Task.CompletedTask;
    public Task OnUpdateErrorAsync(HookContext ctx, Exception ex) => Task.CompletedTask;
    public Task OnBeforeStartAppAsync(HookContext ctx) => Task.CompletedTask;
}

/// <summary>Unix permission hooks — chmod +x main app before start.</summary>
public class UnixPermissionHooks : IUpdateHooks
{
    public async Task OnBeforeStartAppAsync(HookContext ctx)
    {
        var mainApp = Path.Combine(ctx.InstallPath, ctx.UpdateAppName);
        if (File.Exists(mainApp))
            await Task.Run(() => Process.Start("chmod", $"+x \"{mainApp}\"").WaitForExit());
    }
    public Task<bool> OnBeforeUpdateAsync(HookContext ctx) => Task.FromResult(true);
    public Task OnDownloadCompletedAsync(DownloadContext ctx) => Task.CompletedTask;
    public Task OnAfterUpdateAsync(HookContext ctx) => Task.CompletedTask;
    public Task OnUpdateErrorAsync(HookContext ctx, Exception ex) => Task.CompletedTask;
}

/// <summary>User-supplied permission script hook.</summary>
public class CustomPermissionHooks : IUpdateHooks
{
    private readonly string _scriptPath;
    public CustomPermissionHooks(string scriptPath)
        => _scriptPath = scriptPath ?? throw new ArgumentNullException(nameof(scriptPath));

    public async Task OnBeforeStartAppAsync(HookContext ctx)
    {
        var psi = new ProcessStartInfo(_scriptPath, ctx.InstallPath)
        {
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        using var proc = Process.Start(psi)!;
        await Task.Run(() => proc.WaitForExit());
        if (proc.ExitCode != 0)
            throw new InvalidOperationException(
                $"Permission script '{_scriptPath}' failed (exit {proc.ExitCode})");
    }
    public Task<bool> OnBeforeUpdateAsync(HookContext ctx) => Task.FromResult(true);
    public Task OnDownloadCompletedAsync(DownloadContext ctx) => Task.CompletedTask;
    public Task OnAfterUpdateAsync(HookContext ctx) => Task.CompletedTask;
    public Task OnUpdateErrorAsync(HookContext ctx, Exception ex) => Task.CompletedTask;
}
