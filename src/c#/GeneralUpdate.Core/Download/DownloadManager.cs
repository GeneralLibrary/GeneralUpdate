using GeneralUpdate.Core.Events;
using GeneralUpdate.Core.Events.MutiEventArgs;
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

        public delegate void MutiAllDownloadCompletedEventHandler(object sender, MutiAllDownloadCompletedEventArgs e);

        public event MutiAllDownloadCompletedEventHandler MutiAllDownloadCompleted;

        public delegate void MutiDownloadProgressChangedEventHandler(object csender, MutiDownloadProgressChangedEventArgs e);

        public event MutiDownloadProgressChangedEventHandler MutiDownloadProgressChanged;

        public delegate void MutiAsyncCompletedEventHandler(object sender, MutiDownloadCompletedEventArgs e);

        public event MutiAsyncCompletedEventHandler MutiDownloadCompleted;

        public delegate void MutiDownloadErrorEventHandler(object sender, MutiDownloadErrorEventArgs e);

        public event MutiDownloadErrorEventHandler MutiDownloadError;

        public delegate void MutiDownloadStatisticsEventHandler(object sender, MutiDownloadStatisticsEventArgs e);

        public event MutiDownloadStatisticsEventHandler MutiDownloadStatistics;

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
                MutiAllDownloadCompleted(this, new MutiAllDownloadCompletedEventArgs(true, _failedVersions));
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
                if (_failedVersions.Count > 0) MutiAllDownloadCompleted(this, new MutiAllDownloadCompletedEventArgs(true, _failedVersions));
            }
        }

        public void OnMutiDownloadStatistics(object sender, MutiDownloadStatisticsEventArgs e)
        {
            if (MutiDownloadStatistics != null) this.MutiDownloadStatistics(sender, e);
        }

        public void OnMutiDownloadProgressChanged(object sender, MutiDownloadProgressChangedEventArgs e)
        {
            if (MutiDownloadProgressChanged != null) this.MutiDownloadProgressChanged(sender, e);
        }

        public void OnMutiAsyncCompleted(object sender, MutiDownloadCompletedEventArgs e)
        {
            if (MutiDownloadCompleted != null) this.MutiDownloadCompleted(sender, e);
        }

        public void OnMutiDownloadError(object sender, MutiDownloadErrorEventArgs e)
        {
            if (MutiDownloadError != null) this.MutiDownloadError(sender, e);
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