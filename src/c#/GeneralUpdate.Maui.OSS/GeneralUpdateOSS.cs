using GeneralUpdate.Core.Events;
using GeneralUpdate.Core.Events.OSSArgs;
using GeneralUpdate.Maui.OSS.Domain.Entity;
using GeneralUpdate.Maui.OSS.Strategys;

namespace GeneralUpdate.Maui.OSS
{
    /// <summary>
    /// Update applications based on OSS services.
    /// </summary>
    public sealed class GeneralUpdateOSS
    {
        #region Constructors

        private GeneralUpdateOSS()
        { }

        #endregion

        #region Public Methods

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

        public static void AddListenerDownloadProcess(Action<object, OSSDownloadArgs> callbackAction)
        {
            AddListener(callbackAction);
        }

        #endregion

        #region Private Methods

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
            await strategy.Excute();
        }

        private static void AddListener<TArgs>(Action<object, TArgs> callbackAction) where TArgs : EventArgs
        {
            if (callbackAction != null) EventManager.Instance.AddListener(callbackAction);
        }

        #endregion
    }
}