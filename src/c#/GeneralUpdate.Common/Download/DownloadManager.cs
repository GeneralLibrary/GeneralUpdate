using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace GeneralUpdate.Common.Download
{
    public class DownloadManager(string path, string format, int timeOut)
    {
        #region Private Members

        private readonly ImmutableList<DownloadTask>.Builder _downloadTasksBuilder = ImmutableList.Create<DownloadTask>().ToBuilder();
        private ImmutableList<DownloadTask> _downloadTasks;

        #endregion Private Members

        #region Public Properties

        public List<(object, string)> FailedVersions { get; } = new();

        public string Path => path;

        public string Format => format;

        public int TimeOut => timeOut;

        private ImmutableList<DownloadTask> DownloadTasks => _downloadTasks ?? _downloadTasksBuilder.ToImmutable();

        public event EventHandler<MultiAllDownloadCompletedEventArgs> MultiAllDownloadCompleted;
        public event EventHandler<MultiDownloadCompletedEventArgs> MultiDownloadCompleted;
        public event EventHandler<MultiDownloadErrorEventArgs> MultiDownloadError;
        public event EventHandler<MultiDownloadStatisticsEventArgs> MultiDownloadStatistics;

        #endregion Public Properties

        #region Public Methods

        public async Task LaunchTasksAsync()
        {
            try
            {
                var downloadTasks = DownloadTasks.Select(task => task.LaunchAsync()).ToList();
                await Task.WhenAll(downloadTasks);
                MultiAllDownloadCompleted.Invoke(this, new MultiAllDownloadCompletedEventArgs(true, FailedVersions));
            }
            catch (Exception ex)
            {
                MultiAllDownloadCompleted.Invoke(this, new MultiAllDownloadCompletedEventArgs(false, FailedVersions));
                throw new Exception($"Download manager error: {ex.Message}", ex);
            }
        }

        public void OnMultiDownloadStatistics(object sender, MultiDownloadStatisticsEventArgs e)
        => MultiDownloadStatistics?.Invoke(this, e);

        public void OnMultiAsyncCompleted(object sender, MultiDownloadCompletedEventArgs e)
        => MultiDownloadCompleted?.Invoke(this, e);

        public void OnMultiDownloadError(object sender, MultiDownloadErrorEventArgs e)
        {
            MultiDownloadError?.Invoke(this, e);
            FailedVersions.Add((e.Version, e.Exception.Message));
        }

        public void Add(DownloadTask task)
        {
            Debug.Assert(task != null);
            if (!_downloadTasksBuilder.Contains(task))
            {
                _downloadTasksBuilder.Add(task);
            }
        }

        #endregion Public Methods
    }
}