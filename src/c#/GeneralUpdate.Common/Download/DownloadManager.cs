using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.Immutable;
using GeneralUpdate.Common.Download;

namespace GeneralUpdate.Core.Download
{
    public class DownloadManager<TVersion>
    {
        #region Private Members

        private readonly string _path;
        private readonly string _format;
        private readonly int _timeOut;
        private readonly IList<(object, string)> _failedVersions;
        private ImmutableList<DownloadTask<TVersion>>.Builder _downloadTasksBuilder;
        private ImmutableList<DownloadTask<TVersion>> _downloadTasks;

        #endregion Private Members

        #region Constructors

        public DownloadManager(string path, string format, int timeOut)
        {
            _path = path;
            _format = format;
            _timeOut = timeOut;
            _failedVersions = new List<(object, string)>();
            _downloadTasksBuilder = ImmutableList.Create<DownloadTask<TVersion>>().ToBuilder();
        }

        #endregion Constructors

        #region Public Properties

        public IList<(object, string)> FailedVersions => _failedVersions;

        public string Path => _path;

        public string Format => _format;

        public int TimeOut => _timeOut;

        public ImmutableList<DownloadTask<TVersion>> DownloadTasks => _downloadTasks ?? (_downloadTasksBuilder.ToImmutable());

        public event EventHandler<MultiAllDownloadCompletedEventArgs> MultiAllDownloadCompleted;
        public event EventHandler<MultiDownloadProgressChangedEventArgs> MultiDownloadProgressChanged;
        public event EventHandler<MultiDownloadCompletedEventArgs> MultiDownloadCompleted;
        public event EventHandler<MultiDownloadErrorEventArgs> MultiDownloadError;
        public event EventHandler<MultiDownloadStatisticsEventArgs> MultiDownloadStatistics;

        #endregion Public Properties

        #region Public Methods

        public async Task LaunchTasksAsync()
        {
            try
            {
                var downloadTasks = new List<Task>();
                foreach (var task in DownloadTasks)
                {
                    downloadTasks.Add(task.LaunchAsync());
                }
                await Task.WhenAll(downloadTasks);
                MultiAllDownloadCompleted?.Invoke(this, new MultiAllDownloadCompletedEventArgs(true, _failedVersions));
            }
            catch (Exception ex)
            {
                _failedVersions.Add((null, ex.Message));
                MultiAllDownloadCompleted?.Invoke(this, new MultiAllDownloadCompletedEventArgs(false, _failedVersions));
                throw new Exception($"Download manager error: {ex.Message}", ex);
            }
        }

        public void OnMultiDownloadStatistics(object sender, MultiDownloadStatisticsEventArgs e)
        {
            MultiDownloadStatistics?.Invoke(this, e);
        }

        public void OnMultiDownloadProgressChanged(object sender, MultiDownloadProgressChangedEventArgs e)
        {
            MultiDownloadProgressChanged?.Invoke(this, e);
        }

        public void OnMultiAsyncCompleted(object sender, MultiDownloadCompletedEventArgs e)
        {
            MultiDownloadCompleted?.Invoke(this, e);
        }

        public void OnMultiDownloadError(object sender, MultiDownloadErrorEventArgs e)
        {
            MultiDownloadError?.Invoke(this, e);
            _failedVersions.Add((e.Version, e.Exception.Message));
        }

        public void Add(DownloadTask<TVersion> task)
        {
            if (task != null && !_downloadTasksBuilder.Contains(task))
            {
                _downloadTasksBuilder.Add(task);
            }
        }

        public void Remove(DownloadTask<TVersion> task)
        {
            if (task != null && _downloadTasksBuilder.Contains(task))
            {
                _downloadTasksBuilder.Remove(task);
            }
        }

        #endregion Public Methods
    }
}