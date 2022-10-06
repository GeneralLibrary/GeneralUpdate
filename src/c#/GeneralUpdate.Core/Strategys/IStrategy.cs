using GeneralUpdate.Core.Bootstrap;
using GeneralUpdate.Core.Domain.Entity;
using System;

namespace GeneralUpdate.Core.Strategys
{
    /// <summary>
    /// Update the strategy, if you need to extend it, you need to inherit this interface.
    /// </summary>
    public interface IStrategy
    {
        /// <summary>
        /// Execution strategy.
        /// </summary>
        void Excute();

        /// <summary>
        /// Create a policy.
        /// </summary>
        /// <param name="file">Abstraction for updating package information.</param>
        void Create(Entity packet, Action<object, MutiDownloadProgressChangedEventArgs> eventAction, Action<object, ExceptionEventArgs> errorEventAction);

        /// <summary>
        /// After the update is complete.
        /// </summary>
        /// <param name="appName"></param>
        /// <param name="appType"></param>
        /// <returns></returns>
        bool StartApp(string appName, int appType);

        /// <summary>
        /// Get the platform for the current strategy.
        /// </summary>
        /// <returns></returns>
        string GetPlatform();
    }
}