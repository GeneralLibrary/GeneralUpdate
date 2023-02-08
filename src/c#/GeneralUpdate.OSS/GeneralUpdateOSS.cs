using GeneralUpdate.Core.Events;
using GeneralUpdate.OSS.Domain.Entity;
using GeneralUpdate.OSS.OSSStrategys;
using static GeneralUpdate.OSS.Events.OSSEvents;

namespace GeneralUpdate.Core.OSS
{
    /// <summary>
    /// Update applications based on OSS services.
    /// </summary>
    public sealed class GeneralUpdateOSS
    {
        public static Action<long, long> DownloadAction { get; set; }
        public static Action<object, Zip.Events.BaseCompleteEventArgs> UnZipComplete { get; set; }
        public static Action<object, Zip.Events.BaseUnZipProgressEventArgs> UnZipProgress { get; set; }

        private GeneralUpdateOSS(){  }

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
        public static async Task Start<T>(ParamsAndroid parameter) where T : AbstractStrategy, new()
        {
            await BaseStart<T, ParamsAndroid>(parameter);
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
        public static async Task Start<T>(ParamsWindows parameter) where T : AbstractStrategy, new()
        {
            await BaseStart<T, ParamsWindows>(parameter);
        }

        /// <summary>
        /// The underlying update method.
        /// </summary>
        /// <typeparam name="T">The class that needs to be injected with the corresponding platform update policy or inherits the abstract update policy.</typeparam>
        /// <param name="args">List of parameter.</param>
        /// <returns></returns>
        private static async Task BaseStart<T,P>(P parameter) where T : AbstractStrategy , new() where P : class
        {
            //Initialize events that may be used by each platform.
            InitEventManage();
            //Initializes and executes the policy.
            var oss = new T();
            oss.Create(parameter);
            await oss.Excute();
        }

        private static void InitEventManage() 
        {
            EventManager.Instance.AddListener<DownloadEventHandler>((s,e) => { DownloadAction?.Invoke(e.CurrentByte,e.TotalByte); });
            EventManager.Instance.AddListener<UnZipCompletedEventHandler>((s, e) => { UnZipComplete?.Invoke(s,e); });
            EventManager.Instance.AddListener<UnZipProgressEventHandler>((s, e) => { UnZipProgress?.Invoke(s,e); });
        }
    }
}