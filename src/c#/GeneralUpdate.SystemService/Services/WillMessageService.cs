using GeneralUpdate.Core.Domain.PO;
using GeneralUpdate.Core.WillMessage;

namespace GeneralUpdate.SystemService.Services
{
    internal class WillMessageService : BackgroundService
    {
        #region Private Members

        private readonly string? _path;
        private FileSystemWatcher _fileWatcher;

        #endregion

        #region Constructors

        public WillMessageService(IConfiguration configuration) => _path = configuration.GetValue<string>("WatcherPath");

        #endregion

        #region Public Properties
        #endregion

        #region Public Methods

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.Register(() => OnStopping());
            _fileWatcher = new FileSystemWatcher(_path);
            // Watch for changes in LastAccess and LastWrite times, and the renaming of files or directories. 
            _fileWatcher.NotifyFilter = NotifyFilters.LastWrite;
            // Only watch text files.
            _fileWatcher.Filter = "*.*";
            _fileWatcher.Changed += OnChanged;
            _fileWatcher.EnableRaisingEvents = true;
            return Task.CompletedTask;
        }

        #endregion

        #region Private Methods

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            var message = WillMessageManager.Instance.GetWillMessage();
            if (message == null) return;
            switch (message.Status)
            {
                case WillMessageStatus.NotStarted:
                    return;
                case WillMessageStatus.Failed:
                    //WillMessageManager.Instance.Restore();
                    break;
                case WillMessageStatus.Completed:
                    WillMessageManager.Instance.Clear();
                    break;
            }
        }

        private void OnStopping()
        {
            if (_fileWatcher == null) return;
            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher.Dispose();
        }


        #endregion
    }
}
