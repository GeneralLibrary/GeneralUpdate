using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GeneralUpdate.Core.Compress;
using GeneralUpdate.Core.FileSystem;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.JsonContext;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.Strategy;

/// <summary>
/// OSS (Object Storage Service) update strategy.
/// Downloads version configuration, fetches update packages from OSS,
/// decompresses them, and launches the main application.
/// </summary>
/// <remarks>
/// This replaces the legacy <c>OSSStrategy</c> and <c>GeneralUpdateOSS</c> classes.
/// The OSS workflow is OS-agnostic — no platform-specific pipeline is required.
/// </remarks>
public class OSSUpdateStrategy : IStrategy
{
    private GlobalConfigInfo? _configInfo;
    private readonly string _appPath = AppDomain.CurrentDomain.BaseDirectory;
    private const int TimeOut = 60;

    public void Create(GlobalConfigInfo parameter)
    {
        _configInfo = parameter ?? throw new ArgumentNullException(nameof(parameter));
    }

    public async Task ExecuteAsync()
    {
        if (_configInfo == null)
            throw new InvalidOperationException("OSSUpdateStrategy not configured. Call Create() first.");

        try
        {
            var versionFileName = $"{_configInfo.MainAppName ?? _configInfo.AppName}_versions.json";

            GeneralTracer.Debug("OSSUpdateStrategy: 1. Reading version configuration file.");
            var jsonPath = Path.Combine(_appPath, versionFileName);
            if (!File.Exists(jsonPath))
                throw new FileNotFoundException(jsonPath);

            GeneralTracer.Debug("OSSUpdateStrategy: 2. Parsing version configuration.");
            var versions = StorageManager.GetJson<List<VersionOSS>>(jsonPath,
                VersionOSSJsonContext.Default.ListVersionOSS);
            if (versions == null || versions.Count == 0)
                throw new InvalidOperationException("No versions found in OSS configuration.");

            versions = versions.OrderBy(v => v.PubTime).ToList();

            GeneralTracer.Debug($"OSSUpdateStrategy: 3. Downloading {versions.Count} version(s).");
            await DownloadVersionsAsync(versions);

            GeneralTracer.Debug("OSSUpdateStrategy: 4. Decompressing packages.");
            Decompress(versions);

            GeneralTracer.Debug("OSSUpdateStrategy: 5. Launching main application.");
            StartApp();
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("OSSUpdateStrategy.ExecuteAsync failed.", ex);
            throw;
        }
    }

    public void Execute()
    {
        ExecuteAsync().GetAwaiter().GetResult();
    }

    public void StartApp()
    {
        var appName = _configInfo?.MainAppName ?? _configInfo?.AppName;
        if (string.IsNullOrEmpty(appName)) return;

        var appPath = Path.Combine(_appPath, appName);
        if (!File.Exists(appPath))
            throw new FileNotFoundException($"Application not found: {appPath}");

        Process.Start(appPath);
        GeneralTracer.Debug("OSSUpdateStrategy: application started.");
    }

    private async Task DownloadVersionsAsync(List<VersionOSS> versions)
    {
        var manager = new DownloadManager(_appPath, Format.ZIP, TimeOut);
        foreach (var versionInfo in versions)
        {
            var version = new VersionInfo
            {
                Name = versionInfo.PacketName,
                Version = versionInfo.Version,
                Url = versionInfo.Url,
                Format = Format.ZIP,
                Hash = versionInfo.Hash
            };
            manager.Add(new DownloadTask(manager, version));
        }

        await manager.LaunchTasksAsync();
    }

    private void Decompress(List<VersionOSS> versions)
    {
        var encoding = Encoding.GetEncoding(_configInfo?.Encoding?.CodePage ?? Encoding.UTF8.CodePage);
        foreach (var version in versions)
        {
            var zipFilePath = Path.Combine(_appPath, $"{version.PacketName}{Format.ZIP}");
            CompressProvider.Decompress(Format.ZIP, zipFilePath, _appPath, encoding);

            if (!File.Exists(zipFilePath)) continue;
            File.SetAttributes(zipFilePath, FileAttributes.Normal);
            File.Delete(zipFilePath);
        }
    }
}
