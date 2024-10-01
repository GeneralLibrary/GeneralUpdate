using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.ClientCore.Internal;
using GeneralUpdate.ClientCore.Strategys;
using GeneralUpdate.Common.Download;
using GeneralUpdate.Common.Internal.Bootstrap;
using GeneralUpdate.Common.Internal.Event;
using GeneralUpdate.Common.Internal.Strategy;
using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Common.Shared.Service;

namespace GeneralUpdate.ClientCore;

/// <summary>
///     This component is used only for client application bootstrapping classes.
/// </summary>
public class GeneralClientBootstrap : AbstractBootstrap<GeneralClientBootstrap, IStrategy>
{
    #region Public Properties

    /// <summary>
    ///     All update actions of the core object for automatic upgrades will be related to the Packet object.
    /// </summary>
    private Packet Packet { get; }

    #endregion

    #region Private Members

    private IStrategy _strategy;

    private Func<bool> _customSkipOption;

    private readonly List<Func<bool>> _customOptions = new();

    #endregion

    #region Public Methods

    /// <summary>
    ///     Main function for booting the update startup.
    /// </summary>
    /// <returns></returns>
    public override async Task<GeneralClientBootstrap> LaunchAsync()
    {
        ExecuteCustomOptions();
        await InitializeData();
        var manager = new DownloadManager(Packet.InstallPath, Packet.Format, 30);
        foreach (var versionInfo in Packet.UpdateVersions) 
            manager.Add(new DownloadTask(manager, versionInfo));
        
        await manager.LaunchTasksAsync();
        return this;
    }

    /// <summary>
    ///     Configure server address (Recommended Windows,Linux,Mac).
    /// </summary>
    /// <param name="url">Remote server address.</param>
    /// <param name="appName">The updater name does not need to contain an extension.</param>
    /// <returns></returns>
    /// <exception cref="Exception">Parameter initialization is abnormal.</exception>
    public GeneralClientBootstrap SetConfig(string url, string appSecretKey, string appName)
    {
        if (string.IsNullOrEmpty(url)) throw new Exception("Url cannot be empty !");
        var basePath = Thread.GetDomain().BaseDirectory;
        Packet.InstallPath = basePath;
        Packet.AppSecretKey = appSecretKey;
        //update app.
        Packet.AppName = appName;
        var clientVersion = GetFileVersion(Path.Combine(basePath, $"{Packet.AppName}.exe"));
        Packet.ClientVersion = clientVersion;
        Packet.AppType = AppType.ClientApp;
        Packet.UpdateUrl = $"{url}/versions/{AppType.ClientApp}/{clientVersion}/{Packet.AppSecretKey}";
        //main app.
        var mainAppName = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName);
        var mainVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        Packet.MainUpdateUrl = $"{url}/versions/{AppType.ClientApp}/{mainVersion}/{Packet.AppSecretKey}";
        Packet.MainAppName = mainAppName;
        return this;
    }

    /// <summary>
    ///     Custom Configuration (Recommended : All platforms).
    /// </summary>
    /// <param name="info"></param>
    /// <returns></returns>
    public GeneralClientBootstrap SetConfig(Configinfo info)
    {
        Packet.AppType = info.AppType;
        Packet.AppName = info.AppName;
        Packet.AppSecretKey = info.AppSecretKey;
        Packet.ClientVersion = info.ClientVersion;
        Packet.UpdateUrl = info.UpdateUrl;
        Packet.MainUpdateUrl = info.MainUpdateUrl;
        Packet.MainAppName = info.MainAppName;
        Packet.InstallPath = info.InstallPath;
        Packet.UpdateLogUrl = info.UpdateLogUrl;
        return this;
    }

    /// <summary>
    ///     Let the user decide whether to update in the state of non-mandatory update.
    /// </summary>
    /// <param name="func">
    ///     Custom function ,Custom actions to let users decide whether to update. true update false do not
    ///     update .
    /// </param>
    /// <returns></returns>
    public GeneralClientBootstrap SetCustomSkipOption(Func<bool> func)
    {
        Contract.Requires(func != null);
        _customSkipOption = func;
        return this;
    }

    /// <summary>
    ///     Add an asynchronous custom operation.
    ///     In theory, any custom operation can be done. It is recommended to register the environment check method to ensure
    ///     that there are normal dependencies and environments after the update is completed.
    /// </summary>
    /// <param name="func"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public GeneralClientBootstrap AddCustomOption(List<Func<bool>> funcs)
    {
        Contract.Requires(funcs != null && funcs.Any());
        _customOptions.AddRange(funcs);
        return this;
    }

    public GeneralClientBootstrap AddListenerMultiAllDownloadCompleted(
        Action<object, MultiAllDownloadCompletedEventArgs> callbackAction)
        => AddListener(callbackAction);

    public GeneralClientBootstrap AddListenerMultiDownloadProgress(
        Action<object, MultiDownloadProgressChangedEventArgs> callbackAction)
        => AddListener(callbackAction);

    public GeneralClientBootstrap AddListenerMultiDownloadCompleted(
        Action<object, MultiDownloadCompletedEventArgs> callbackAction)
        => AddListener(callbackAction);

    public GeneralClientBootstrap AddListenerMultiDownloadError(
        Action<object, MultiDownloadErrorEventArgs> callbackAction)
        => AddListener(callbackAction);

    public GeneralClientBootstrap AddListenerMultiDownloadStatistics(
        Action<object, MultiDownloadStatisticsEventArgs> callbackAction)
        => AddListener(callbackAction);

    public GeneralClientBootstrap AddListenerException(Action<object, ExceptionEventArgs> callbackAction)
        => AddListener(callbackAction);

    #endregion Public Methods

    #region Private Methods

    private async Task InitializeData()
    {
        ClearEnvironmentVariable();

        //Request the upgrade information needed by the client and upgrade end, and determine if an upgrade is necessary.
        var versionService = new VersionService();
        var mainResp = await versionService.ValidationVersion(Packet.MainUpdateUrl);
        var upgradeResp = await versionService.ValidationVersion(Packet.UpdateUrl);

        Packet.IsUpgradeUpdate = upgradeResp.Body.IsUpdate;
        Packet.IsMainUpdate = mainResp.Body.IsUpdate;
        //No need to update, return directly.
        if (!Packet.IsMainUpdate && !Packet.IsUpgradeUpdate) return;

        //If the main program needs to be forced to update, the skip will not take effect.
        var isForcibly = mainResp.Body.IsForcibly || upgradeResp.Body.IsForcibly;
        if (CanSkip(isForcibly)) return;

        Packet.UpdateVersions = VersionAssembler.ToEntitys(upgradeResp.Body.Versions);
        Packet.LastVersion = Packet.UpdateVersions.Last().Version;

        //Initialize the process transfer parameter object.
        var processInfo = new ProcessInfo(Packet.MainAppName
            , Packet.InstallPath
            , Packet.ClientVersion
            , Packet.LastVersion
            , Packet.UpdateLogUrl
            , Packet.Encoding
            , Packet.Format
            , Packet.DownloadTimeOut
            , Packet.AppSecretKey
            , mainResp.Body.Versions);
        Packet.ProcessInfo = JsonSerializer.Serialize(processInfo);
    }

    /// <summary>
    ///     Gets the application version number
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    /// <exception cref="GeneralUpdateException{ExceptionArgs}"></exception>
    private string GetFileVersion(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Exists) return FileVersionInfo.GetVersionInfo(filePath).FileVersion;
        throw new FileNotFoundException($"Failed to obtain file '{filePath}' version. Procedure.");
    }

    /// <summary>
    ///     User decides if update is required.
    /// </summary>
    /// <returns>is false to continue execution.</returns>
    private bool CanSkip(bool isForcibly)
    {
        if (isForcibly) return false;
        Contract.Requires(_customSkipOption != null);
        return _customSkipOption.Invoke();
    }

    /// <summary>
    ///     Performs all injected custom operations.
    /// </summary>
    /// <returns></returns>
    private void ExecuteCustomOptions()
    {
        Contract.Requires(_customOptions != null && _customOptions.Any());
        foreach (var option in _customOptions)
            if (!option.Invoke())
                EventManager.Instance.Dispatch(this,
                    new ExceptionEventArgs(null, $"{nameof(option)}Execution failure!"));
    }

    /// <summary>
    ///     Clear the environment variable information needed to start the upgrade assistant process.
    /// </summary>
    private void ClearEnvironmentVariable()
    {
        try
        {
            Environment.SetEnvironmentVariable("ProcessInfo", null, EnvironmentVariableTarget.User);
        }
        catch (SecurityException ex)
        {
            EventManager.Instance.Dispatch(this,
                new ExceptionEventArgs(ex,
                    "Error: You do not have sufficient permissions to delete this environment variable."));
        }
        catch (ArgumentException ex)
        {
            EventManager.Instance.Dispatch(this,
                new ExceptionEventArgs(ex, "Error: The environment variable name is invalid."));
        }
        catch (IOException ex)
        {
            EventManager.Instance.Dispatch(this,
                new ExceptionEventArgs(ex,
                    "Error: An I/O error occurred while deleting the environment variable."));
        }
        catch (Exception ex)
        {
            EventManager.Instance.Dispatch(this,
                new ExceptionEventArgs(ex,
                    "Error: An unknown error occurred while deleting the environment variable."));
        }
    }

    protected override void ExecuteStrategy()
    {
        _strategy.Create(Packet);
        _strategy.Execute();
    }

    protected override GeneralClientBootstrap StrategyFactory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            _strategy = new WindowsStrategy();
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            _strategy = new LinuxStrategy();
        else
            throw new PlatformNotSupportedException("The current operating system is not supported!");

        return this;
    }

    private GeneralClientBootstrap AddListener<TArgs>(Action<object, TArgs> callbackAction) where TArgs : EventArgs
    {
        Contract.Requires(callbackAction != null);
        EventManager.Instance.AddListener(callbackAction);
        return this;
    }

    private void OnMultiDownloadStatistics(object sender, MultiDownloadStatisticsEventArgs e)
        => EventManager.Instance.Dispatch(sender, e);

    private void OnMultiDownloadProgressChanged(object sender, MultiDownloadProgressChangedEventArgs e)
        => EventManager.Instance.Dispatch(sender, e);

    private void OnMultiDownloadCompleted(object sender, MultiDownloadCompletedEventArgs e)
        => EventManager.Instance.Dispatch(sender, e);

    private void OnMultiDownloadError(object sender, MultiDownloadErrorEventArgs e)
    => EventManager.Instance.Dispatch(sender, e);

    private void OnMultiAllDownloadCompleted(object sender, MultiAllDownloadCompletedEventArgs e)
    {
        EventManager.Instance.Dispatch(sender, e);
        ExecuteStrategy();
    }

    #endregion Private Methods
}