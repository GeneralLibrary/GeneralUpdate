using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using GeneralUpdate.Common.Download;
using GeneralUpdate.Common.FileBasic;
using GeneralUpdate.Common.Internal;
using GeneralUpdate.Common.Internal.Event;
using GeneralUpdate.Common.Internal.Strategy;
using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Core.Internal;
using GeneralUpdate.Core.Strategys;

namespace GeneralUpdate.Core
{
    public sealed class GeneralUpdateOSS
    {
        private GeneralUpdateOSS() { }

        #region Public Methods

        /// <summary>
        /// Starting an OSS update for windows,Linux,mac platform.
        /// </summary>
        /// <returns></returns>
        public static async Task Start()=> await BaseStart();

        public static void AddListenerMultiAllDownloadCompleted(Action<object, MultiAllDownloadCompletedEventArgs> callbackAction)
            => AddListener(callbackAction);

        public static void AddListenerMultiDownloadProgress(Action<object, MultiDownloadProgressChangedEventArgs> callbackAction)
            => AddListener(callbackAction);

        public static void AddListenerMultiDownloadCompleted(Action<object, MultiDownloadCompletedEventArgs> callbackAction)
            => AddListener(callbackAction);

        public static void AddListenerMultiDownloadError(Action<object, MultiDownloadErrorEventArgs> callbackAction)
            => AddListener(callbackAction);

        public static void AddListenerMultiDownloadStatistics(Action<object, MultiDownloadStatisticsEventArgs> callbackAction)
            => AddListener(callbackAction);

        public static void AddListenerException(Action<object, ExceptionEventArgs> callbackAction)
            => AddListener(callbackAction);

        public static void AddListenerDownloadConfigProcess(Action<object, OSSDownloadArgs> callbackAction)
            => AddListener(callbackAction);

        #endregion Public Methods

        #region Private Methods

        private static void AddListener<TArgs>(Action<object, TArgs> callbackAction) where TArgs : EventArgs
        {
            Debug.Assert(callbackAction != null);
            EventManager.Instance.AddListener(callbackAction);
        }

        /// <summary>
        /// The underlying update method.
        /// </summary>
        /// <typeparam name="T">The class that needs to be injected with the corresponding platform update policy or inherits the abstract update policy.</typeparam>
        /// <param name="args">List of parameter.</param>
        /// <returns></returns>
        private static async Task BaseStart()
        {
            var json = Environment.GetEnvironmentVariable("ParamsOSS", EnvironmentVariableTarget.User);
            if (string.IsNullOrWhiteSpace(json))
                return;

            var parameter = JsonSerializer.Deserialize<ParamsOSS>(json);
            var strategy = new OSSStrategy();
            strategy.Create(parameter);
            await strategy.ExecuteAsync();
        }
        
        #endregion Private Methods
    }
}