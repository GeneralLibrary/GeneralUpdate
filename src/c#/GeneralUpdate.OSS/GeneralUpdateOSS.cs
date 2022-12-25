using GeneralUpdate.OSS.Strategys;

namespace GeneralUpdate.Core.OSS
{
    /// <summary>
    /// Update applications based on OSS services.
    /// </summary>
    public sealed class GeneralUpdateOSS
    {
        /// <summary>
        /// Starting an OSS update.
        /// </summary>
        /// <typeparam name="T">The class that needs to be injected with the corresponding platform update policy or inherits the abstract update policy.</typeparam>
        /// <param name="url">Remote server address.</param>
        /// <param name="appName">Application name.</param>
        /// <param name="currentVersion">Version number of the current application.</param>
        /// <param name="authority">Application authority.</param>
        /// <param name="versionFileName">Updated version configuration file name.</param>
        /// <exception cref="ArgumentNullException">Method entry parameter is null exception.</exception>
        public static void Start<T>(string url, string appName,string currentVersion, string authority, string versionFileName = "versions.json") where T : AbstractStrategy, new()
        {
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(appName) || 
                string.IsNullOrWhiteSpace(currentVersion) || string.IsNullOrWhiteSpace(authority)) 
                throw new ArgumentNullException("The parameter cannot be empty !");
            //Initializes and executes the policy.
            var oss = new T();
            oss.Create(url, appName, currentVersion, authority, versionFileName);
            oss.Excute();
        }
    }
}
