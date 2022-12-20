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
   //     public static void Start<T>(string url,string appName ,string fileName = "versions.json" , string format = Format.ZIP, int timeOut = 60, string platform = PlatformType.Windows) where T : IOSS, new()
   //     {
   //         if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(appName)) throw new ArgumentNullException("The parameter cannot be empty !");
			//try
			//{
   //             IStrategy strategy = null;
   //             var oss = new T();
   //             oss.SetParameter(url, fileName, format, timeOut);
   //             oss.Update();
   //             switch (platform)
   //             {
   //                 case PlatformType.Windows:
   //                     strategy = new WindowsStrategy();
   //                     break;
   //                 case PlatformType.Android:
   //                     strategy = new AndoridStrategy();
   //                     break;
   //             }
   //             strategy.Create(appName);
   //             strategy.Excute();
   //             strategy.StartApp();
   //         }
			//catch (Exception ex)
			//{
			//	throw new GeneralUpdateException<ExceptionArgs>($"Update failed , { ex.Message } !", ex.InnerException);
			//}
   //     }
    }
}
