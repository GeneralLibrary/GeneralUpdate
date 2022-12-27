using GeneralUpdate.OSS.Domain.Entity;
using GeneralUpdate.OSS.Strategys;

namespace GeneralUpdate.Core.OSS
{
    /// <summary>
    /// Update applications based on OSS services.
    /// </summary>
    public sealed class GeneralUpdateOSS
    {
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
        public static async Task Start<T>(ParamsAndroid @params) where T : AbstractStrategy, new()
        {
            await BaseStart<T>(@params.Url, @params.Apk, @params.CurrentVersion, @params.Authority, @params.VersionFileName);
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
        public static async Task Start<T>(ParamsWindows @params) where T : AbstractStrategy, new()
        {
            await BaseStart<T>(@params.Url, @params.AppName, @params.CurrentVersion, @params.VersionFileName);
        }

        /// <summary>
        /// The underlying update method.
        /// </summary>
        /// <typeparam name="T">The class that needs to be injected with the corresponding platform update policy or inherits the abstract update policy.</typeparam>
        /// <param name="args">List of parameters.</param>
        /// <returns></returns>
        private static async Task BaseStart<T>(params string[] args) where T : AbstractStrategy, new()
        {
            //Initializes and executes the policy.
            var oss = new T();
            oss.Create(args);
            await oss.Excute();
        }
    }
}
