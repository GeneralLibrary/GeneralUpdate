using GeneralUpdate.Core.Domain.Entity;
using GeneralUpdate.Core.Events;
using GeneralUpdate.Core.Events.CommonArgs;
using GeneralUpdate.Core.Events.MultiEventArgs;
using GeneralUpdate.Core.Events.OSSArgs;
using GeneralUpdate.Core.Strategys;
using System;
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
        /// Starting an OSS update for windows,linux,mac platform.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public static async Task Start<TStrategy>(ParamsOSS parameter) where TStrategy : AbstractStrategy, new()
        {
            await BaseStart<TStrategy, ParamsOSS>(parameter);
        }

        public void AddListenerMultiAllDownloadCompleted(Action<object, MultiAllDownloadCompletedEventArgs> callbackAction)
        {
            AddListener(callbackAction);
        }

        public void AddListenerMultiDownloadProgress(Action<object, MultiDownloadProgressChangedEventArgs> callbackAction)
        {
            AddListener(callbackAction);
        }

        public void AddListenerMultiDownloadCompleted(Action<object, MultiDownloadCompletedEventArgs> callbackAction)
        {
            AddListener(callbackAction);
        }

        public void AddListenerMultiDownloadError(Action<object, MultiDownloadErrorEventArgs> callbackAction)
        {
            AddListener(callbackAction);
        }

        public void AddListenerMultiDownloadStatistics(Action<object, MultiDownloadStatisticsEventArgs> callbackAction)
        {
            AddListener(callbackAction);
        }

        public void AddListenerException(Action<object, ExceptionEventArgs> callbackAction)
        {
            AddListener(callbackAction);
        }

        public void AddListenerDownloadConfigProcess(Action<object, OSSDownloadArgs> callbackAction)
        {
            AddListener(callbackAction);
        }

        #endregion

        #region Private Methods

        private void AddListener<TArgs>(Action<object, TArgs> callbackAction) where TArgs : EventArgs
        {
            if (callbackAction != null) EventManager.Instance.AddListener(callbackAction);
        }

        /// <summary>
        /// The underlying update method.
        /// </summary>
        /// <typeparam name="T">The class that needs to be injected with the corresponding platform update policy or inherits the abstract update policy.</typeparam>
        /// <param name="args">List of parameter.</param>
        /// <returns></returns>
        private static async Task BaseStart<TStrategy, TParams>(TParams parameter) where TStrategy : AbstractStrategy, new() where TParams : class
        {
            //Initializes and executes the policy.
            var strategyFunc = new Func<TStrategy>(() => new TStrategy());
            var strategy = strategyFunc();
            strategy.Create(parameter);
            //Implement different update strategies depending on the platform.
            await strategy.ExecuteTaskAsync();
        }

        #endregion
    }
}