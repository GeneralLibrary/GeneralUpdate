using System;
using System.Collections.Generic;
using System.Text;

namespace GeneralUpdate.Core.OSS.Strategys
{
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
        void Create();

        /// <summary>
        /// After the update is complete.
        /// </summary>
        /// <param name="appName"></param>
        /// <returns></returns>
        bool StartApp(string appName);

        /// <summary>
        /// Get the platform for the current strategy.
        /// </summary>
        /// <returns></returns>
        string GetPlatform();
    }
}
