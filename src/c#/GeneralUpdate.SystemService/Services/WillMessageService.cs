using GeneralUpdate.SystemService.PersistenceObjects;

namespace GeneralUpdate.SystemService.Services
{
    internal class WillMessageService : BackgroundService
    {
        private readonly string? _path;
        private FileSystemWatcher _fileWatcher;

        public WillMessageService(IConfiguration configuration) => _path = configuration.GetValue<string>("WatcherPath");

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

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            var willMessage = new WillMessagePersistence<ProcessPersistence>();
            switch (willMessage.Status)
            {
                case ProcessStatus.NotStarted:
                    break;
                case ProcessStatus.Failed:
                    break;
                case ProcessStatus.Completed: 
                    break;
            }
        }

        private void OnStopping()
        {
            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher.Dispose();
        }
    }
}
