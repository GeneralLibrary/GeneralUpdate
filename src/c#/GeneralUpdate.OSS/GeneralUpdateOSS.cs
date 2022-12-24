using GeneralUpdate.OSS.Strategys;

namespace GeneralUpdate.Core.OSS
{
    public sealed class GeneralUpdateOSS
    {
        /// <summary>
        /// Start OSS update.
        /// </summary>
        /// <typeparam name="T">OSS SDK or Custom OSS</typeparam>
        /// <param name="url">url address</param>
        /// <param name="appName">main app name</param>
        /// <param name="platform">platform enum</param>
        /// <returns></returns>
        public static void Start<T>(string url, string appName, string authority, string versionFileName = "versions.json") where T : AbstractStrategy, new()
        {
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(appName)) throw new ArgumentNullException("The parameter cannot be empty !");
            var oss = new T();
            oss.Create(url, appName, authority, versionFileName);
            oss.Excute();
        }
    }
}
