using GeneralUpdate.Core.WillMessage;
using System.Diagnostics;

#if WINDOWS
using System.Runtime.InteropServices;
#endif

namespace GeneralUpdate.SystemService.Services
{
    internal class WillMessageService : BackgroundService
    {
        #region Private Members

#if WINDOWS
        const uint SMTO_ABORTIFHUNG = 0x0002;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, UIntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out UIntPtr lpdwResult);
#endif

        private readonly string? _path;
        private FileSystemWatcher _fileWatcher;
        private ILogger<WillMessageService> _logger;

        #endregion Private Members

        #region Constructors

        public WillMessageService(IConfiguration configuration, ILogger<WillMessageService> logger)
        {
            _path = configuration.GetValue<string>("WatcherPath");
            _logger = logger;
        }

        #endregion Constructors

        #region Public Methods

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Will message check executed.");
                WillMessageManager.Instance.Check();
            }
            catch (Exception ex)
            {
                _logger.LogError($"StartAsync error: {ex}!");
            }
            return base.StartAsync(cancellationToken);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("File watcher executed.");
                stoppingToken.Register(() => OnStopping());
                _fileWatcher = new FileSystemWatcher(_path);
                // Watch for changes in LastAccess and LastWrite times, and the renaming of files or directories.
                _fileWatcher.NotifyFilter = NotifyFilters.LastWrite;
                // Only watch text files.
                _fileWatcher.Filter = "*.*";
                _fileWatcher.Changed += OnChanged;
                _fileWatcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"ExecuteAsync error: {ex}!");
            }
            return Task.CompletedTask;
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Will message clear executed.");
                WillMessageManager.Instance.Clear();
            }
            catch (Exception ex)
            {
                _logger.LogError($"StopAsync error: {ex}!");
            }
            return base.StopAsync(cancellationToken);
        }

        #endregion Public Methods

        #region Private Methods

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                _logger.LogInformation("Will message check executed.");
                WillMessageManager.Instance.Check();
            }
            catch (Exception ex)
            {
                _logger.LogError($"OnChanged error:{ex}");
            }
        }

        private void OnStopping()
        {
            if (_fileWatcher == null) return;
            try
            {
                _logger.LogInformation("OnStopping executed.");
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError($"OnStopping error:{ex}");
            }
        }

        private void Diagnosis(string processName)
        {
            try
            {
                Process[] processes = Process.GetProcesses();
                foreach (Process p in processes)
                {
                    if (string.Equals(processName, p.ProcessName, StringComparison.OrdinalIgnoreCase) && !p.MainWindowHandle.Equals(IntPtr.Zero))
                    {
#if WINDOWS
                        UIntPtr result;
                        IntPtr sendResult = SendMessageTimeout(p.MainWindowHandle, 0x0, UIntPtr.Zero, IntPtr.Zero, SMTO_ABORTIFHUNG, 3000, out result);
                        bool notResponding = sendResult == IntPtr.Zero;
                        _logger.LogInformation($"Process: {p.ProcessName}, Responding: {!notResponding}");
#endif
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Diagnosis error:{ex}");
            }
        }

        #endregion Private Methods
    }
}