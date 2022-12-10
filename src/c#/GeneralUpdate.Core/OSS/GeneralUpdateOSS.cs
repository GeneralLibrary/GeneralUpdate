using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.Exceptions.CustomArgs;
using GeneralUpdate.Core.Exceptions.CustomException;
using GeneralUpdate.Core.OSS.Strategys;
using GeneralUpdate.Core.OSS.Strategys.PlatformAndorid;
using GeneralUpdate.Core.OSS.Strategys.PlatformWindows;
using System;
using System.Threading.Tasks;

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
        public static async Task Start<T>(string url,string appName , string platform = PlatformType.Windows) where T : IOSS, new()
        {
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(appName)) throw new ArgumentNullException("The parameter cannot be empty !");
			try
			{
                IStrategy strategy = null;
                var oss = await new T().Download(url);
                switch (platform)
                {
                    case PlatformType.Windows:
                        strategy = new WindowsStrategy();
                        break;
                    case PlatformType.Android:
                        strategy = new AndoridStrategy();
                        break;
                }
                strategy.Create(appName);
                strategy.Excute();
                strategy.StartApp();
            }
			catch (Exception ex)
			{
				throw new GeneralUpdateException<ExceptionArgs>($"Update failed , { ex.Message } !", ex.InnerException);
			}
        }
    }
}
