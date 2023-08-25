using GeneralUpdate.Core.Domain.Entity;
using GeneralUpdate.Core.Events;
using GeneralUpdate.Core.Events.CommonArgs;
using GeneralUpdate.Core.Events.MultiEventArgs;
using GeneralUpdate.Core.Events.OSSArgs;
using GeneralUpdate.Core.Strategys;
using System;
using System.Text;
using System.Threading.Tasks;

namespace GeneralUpdate.Core
{
    public sealed class GeneralUpdateOSS
    {
        #region Constructors

        private GeneralUpdateOSS()
        { }

        #endregion

        #region Public Methods

        /// <summary>
        /// Starting an OSS update for windows,Linux,mac platform.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public static async Task Start<TStrategy>(ParamsOSS parameter,Encoding encoding) where TStrategy : AbstractStrategy, new()
        {
            await BaseStart<TStrategy, ParamsOSS>(parameter, encoding);
        }

        public static void AddListenerMultiAllDownloadCompleted(Action<object, MultiAllDownloadCompletedEventArgs> callbackAction)
        {
            AddListener(callbackAction);
        }

        public static void AddListenerMultiDownloadProgress(Action<object, MultiDownloadProgressChangedEventArgs> callbackAction)
        {
            AddListener(callbackAction);
        }

        public static void AddListenerMultiDownloadCompleted(Action<object, MultiDownloadCompletedEventArgs> callbackAction)
        {
            AddListener(callbackAction);
        }

        public static void AddListenerMultiDownloadError(Action<object, MultiDownloadErrorEventArgs> callbackAction)
        {
            AddListener(callbackAction);
        }

        public static void AddListenerMultiDownloadStatistics(Action<object, MultiDownloadStatisticsEventArgs> callbackAction)
        {
            AddListener(callbackAction);
        }

        public static void AddListenerException(Action<object, ExceptionEventArgs> callbackAction)
        {
            AddListener(callbackAction);
        }

        public static void AddListenerDownloadConfigProcess(Action<object, OSSDownloadArgs> callbackAction)
        {
            AddListener(callbackAction);
        }

        #endregion

        #region Private Methods

        private static void AddListener<TArgs>(Action<object, TArgs> callbackAction) where TArgs : EventArgs
        {
            if (callbackAction != null) EventManager.Instance.AddListener(callbackAction);
        }

        /// <summary>
        /// The underlying update method.
        /// </summary>
        /// <typeparam name="T">The class that needs to be injected with the corresponding platform update policy or inherits the abstract update policy.</typeparam>
        /// <param name="args">List of parameter.</param>
        /// <returns></returns>
        private static async Task BaseStart<TStrategy, TParams>(TParams parameter,Encoding encoding) where TStrategy : AbstractStrategy, new() where TParams : class
        {
            //Initializes and executes the policy.
            var strategyFunc = new Func<TStrategy>(() => new TStrategy());
            var strategy = strategyFunc();
            strategy.Create(parameter, encoding);
            //Implement different update strategies depending on the platform.
            await strategy.ExecuteTaskAsync();
        }

        #endregion
    }
}