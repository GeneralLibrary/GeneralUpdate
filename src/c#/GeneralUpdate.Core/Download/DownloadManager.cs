using GeneralUpdate.Core.Events.MultiEventArgs;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Download
{
    /// <summary>
    /// download task manager.
    /// </summary>
    /// <typeparam name="T">update version infomation.</typeparam>
    public sealed class DownloadManager<TVersion> : AbstractTaskManager<TVersion>
    {
        #region Private Members

        private string _path;
        private string _format;
        private int _timeOut;
        private IList<(object, string)> _failedVersions;
        private ImmutableList<ITask<TVersion>>.Builder _downloadTasksBuilder;
        private ImmutableList<ITask<TVersion>> _downloadTasks;

        #endregion Private Members

        #region Constructors

        /// <summary>
        ///  download manager constructors.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="format"></param>
        /// <param name="timeOut"></param>
        public DownloadManager(string path, string format, int timeOut)
        {
            _path = path;
            _format = format;
            _timeOut = timeOut;
            _failedVersions = new List<ValueTuple<object, string>>();
            _downloadTasksBuilder = ImmutableList.Create<ITask<TVersion>>().ToBuilder();
        }

        #endregion Constructors

        #region Public Properties

        /// <summary>
        /// Record download exception information for all versions.
        /// object: is 'UpdateVersion' , string: is error infomation.
        /// </summary>
        public IList<(object, string)> FailedVersions { get => _failedVersions; }

        public string Path { get => _path; }

        public string Format { get => _format; }

        public int TimeOut { get => _timeOut; }

        public ImmutableList<ITask<TVersion>> DownloadTasks { get => _downloadTasks ?? (_downloadTasksBuilder.ToImmutable()); private set => _downloadTasks = value; }

        public delegate void MultiAllDownloadCompletedEventHandler(object sender, MultiAllDownloadCompletedEventArgs e);

        public event MultiAllDownloadCompletedEventHandler MultiAllDownloadCompleted;

        public delegate void MultiDownloadProgressChangedEventHandler(object csender, MultiDownloadProgressChangedEventArgs e);

        public event MultiDownloadProgressChangedEventHandler MultiDownloadProgressChanged;

        public delegate void MultiAsyncCompletedEventHandler(object sender, MultiDownloadCompletedEventArgs e);

        public event MultiAsyncCompletedEventHandler MultiDownloadCompleted;

        public delegate void MultiDownloadErrorEventHandler(object sender, MultiDownloadErrorEventArgs e);

        public event MultiDownloadErrorEventHandler MultiDownloadError;

        public delegate void MultiDownloadStatisticsEventHandler(object sender, MultiDownloadStatisticsEventArgs e);

        public event MultiDownloadStatisticsEventHandler MultiDownloadStatistics;

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// launch update.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="AmbiguousMatchException"></exception>
        public void LaunchTaskAsync()
        {
            try
            {
                var downloadTasks = new List<Task>();
                foreach (var task in DownloadTasks)
                {
                    var downloadTask = (task as DownloadTask<TVersion>);
                    downloadTasks.Add(downloadTask.Launch());
                }
                Task.WaitAll(downloadTasks.ToArray());
                MultiAllDownloadCompleted(this, new MultiAllDownloadCompletedEventArgs(true, _failedVersions));
            }
            catch (ObjectDisposedException ex)
            {
                throw new ArgumentNullException("Download manager launch abnormally ! exception is 'ObjectDisposedException'.", ex);
            }
            catch (AggregateException ex)
            {
                throw new ArgumentNullException("Download manager launch abnormally ! exception is 'AggregateException'.", ex);
            }
            catch (ArgumentNullException ex)
            {
                throw new ArgumentNullException("Download manager launch abnormally ! exception is 'ArgumentNullException'.", ex);
            }
            catch (AmbiguousMatchException ex)
            {
                throw new AmbiguousMatchException("Download manager launch abnormally ! exception is 'AmbiguousMatchException'.", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Download manager error : {ex.Message} !", ex.InnerException);
            }
            finally
            {
                if (_failedVersions.Count > 0) MultiAllDownloadCompleted(this, new MultiAllDownloadCompletedEventArgs(true, _failedVersions));
            }
        }

        public void OnMultiDownloadStatistics(object sender, MultiDownloadStatisticsEventArgs e)
        {
            if (MultiDownloadStatistics != null) this.MultiDownloadStatistics(sender, e);
        }

        public void OnMultiDownloadProgressChanged(object sender, MultiDownloadProgressChangedEventArgs e)
        {
            if (MultiDownloadProgressChanged != null) this.MultiDownloadProgressChanged(sender, e);
        }

        public void OnMultiAsyncCompleted(object sender, MultiDownloadCompletedEventArgs e)
        {
            if (MultiDownloadCompleted != null) this.MultiDownloadCompleted(sender, e);
        }

        public void OnMultiDownloadError(object sender, MultiDownloadErrorEventArgs e)
        {
            if (MultiDownloadError != null) this.MultiDownloadError(sender, e);
            _failedVersions.Add((e.Version, e.Exception.Message));
        }

        public override void Remove(ITask<TVersion> task)
        {
            if (task != null && _downloadTasksBuilder.Contains(task)) _downloadTasksBuilder.Remove(task);
        }

        public override void Add(ITask<TVersion> task)
        {
            if (task != null && !_downloadTasksBuilder.Contains(task)) _downloadTasksBuilder.Add(task);
        }

        #endregion Public Methods
    }
}