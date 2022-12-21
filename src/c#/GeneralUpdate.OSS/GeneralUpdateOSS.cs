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
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="GeneralUpdateException{ExceptionArgs}"></exception>
        public static void Start<T>(string url, string appName, string fileName = "versions.json")
        {
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(appName)) throw new ArgumentNullException("The parameter cannot be empty !");
        }
    }
}
