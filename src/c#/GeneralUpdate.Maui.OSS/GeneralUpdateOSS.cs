using GeneralUpdate.Core.Events;
using GeneralUpdate.Maui.OSS.Domain.Entity;
using GeneralUpdate.Maui.OSS.Events;
using GeneralUpdate.Maui.OSS.Strategys;

namespace GeneralUpdate.Maui.OSS
{
    /// <summary>
    /// Update applications based on OSS services.
    /// </summary>
    public sealed class GeneralUpdateOSS
    {
        public delegate void DownloadEventHandler(object sender, OSSDownloadArgs e);

        public static event DownloadEventHandler Download;

        public delegate void UnZipCompletedEventHandler(object sender, Zip.Events.BaseCompleteEventArgs e);

        public static event UnZipCompletedEventHandler UnZipCompleted;

        public delegate void UnZipProgressEventHandler(object sender, Zip.Events.BaseUnZipProgressEventArgs e);

        public static event UnZipProgressEventHandler UnZipProgress;

        private GeneralUpdateOSS()
        { }

        /// <summary>
        /// Starting an OSS update for android platform.
        /// </summary>
        /// <typeparam name="T">The class that needs to be injected with the corresponding platform update policy or inherits the abstract update policy.</typeparam>
        /// <param name="url">Remote server address.</param>
        /// <param name="apk">apk name.</param>
        /// <param name="currentVersion">Version number of the current application.</param>
        /// <param name="authority">Application authority.</param>
        /// <param name="versionFileName">Updated version configuration file name.</param>
        /// <exception cref="ArgumentNullException">Method entry parameter is null exception.</exception>
        public static async Task Start<TStrategy>(ParamsAndroid parameter) where TStrategy : AbstractStrategy, new()
        {
            await BaseStart<TStrategy, ParamsAndroid>(parameter);
        }

        /// <summary>
        /// Starting an OSS update for windows platform.
        /// </summary>
        /// <typeparam name="T">The class that needs to be injected with the corresponding platform update policy or inherits the abstract update policy.</typeparam>
        /// <param name="url">Remote server address.</param>
        /// <param name="appName">Application name.</param>
        /// <param name="currentVersion">Version number of the current application.</param>
        /// <param name="versionFileName">Updated version configuration file name.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static async Task Start<TStrategy>(ParamsWindows parameter) where TStrategy : AbstractStrategy, new()
        {
            await BaseStart<TStrategy, ParamsWindows>(parameter);
        }

        /// <summary>
        /// Starting an OSS update for windows,linux,mac platform.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public static async Task Start<TStrategy>(ParamsOSS parameter) where TStrategy : AbstractStrategy, new()
        {
            await BaseStart<TStrategy, ParamsOSS>(parameter);
        }

        /// <summary>
        /// The underlying update method.
        /// </summary>
        /// <typeparam name="T">The class that needs to be injected with the corresponding platform update policy or inherits the abstract update policy.</typeparam>
        /// <param name="args">List of parameter.</param>
        /// <returns></returns>
        private static async Task BaseStart<TStrategy, TParams>(TParams parameter) where TStrategy : AbstractStrategy, new() where TParams : class
        {
            //Initialize events that may be used by each platform.
            InitEventManage();
            //Initializes and executes the policy.
            var strategyFunc = new Func<TStrategy>(() => new TStrategy());
            var strategy = strategyFunc();
            strategy.Create(parameter);
            //Implement different update strategies depending on the platform.
            await strategy.Excute();
        }

        private static void InitEventManage()
        {
            EventManager.Instance.AddListener<DownloadEventHandler>((s, e) =>
            {
                if (Download != null) Download(s, e);
            });
            EventManager.Instance.AddListener<UnZipCompletedEventHandler>((s, e) =>
            {
                if (UnZipCompleted != null) UnZipCompleted(s, e);
            });
            EventManager.Instance.AddListener<UnZipProgressEventHandler>((s, e) =>
            {
                if (UnZipProgress != null) UnZipProgress(s, e);
            });
        }
    }
}