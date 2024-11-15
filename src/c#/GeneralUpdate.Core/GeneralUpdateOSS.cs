using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using GeneralUpdate.Common.AOT.JsonContext;
using GeneralUpdate.Common.Download;
using GeneralUpdate.Common.Internal;
using GeneralUpdate.Common.Internal.Event;
using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Core.Internal;
using GeneralUpdate.Core.Strategys;

namespace GeneralUpdate.Core
{
    public sealed class GeneralUpdateOSS
    {
        private GeneralUpdateOSS() { }

        #region Public Methods

        /// <summary>
        /// Starting an OSS update for windows,Linux,mac platform.
        /// </summary>
        /// <returns></returns>
        public static async Task Start()=> await BaseStart();

        #endregion Public Methods

        #region Private Methods
        
        /// <summary>
        /// The underlying update method.
        /// </summary>
        private static async Task BaseStart()
        {
            try
            {
                var json = Environment.GetEnvironmentVariable("GlobalConfigInfoOSS", EnvironmentVariableTarget.User);
                if (string.IsNullOrWhiteSpace(json))
                    return;

                var parameter = JsonSerializer.Deserialize<GlobalConfigInfoOSS>(json, GlobalConfigInfoOSSJsonContext.Default.GlobalConfigInfoOSS);
                var strategy = new OSSStrategy();
                strategy.Create(parameter);
                await strategy.ExecuteAsync();
            }
            catch (Exception exception)
            {
                throw new Exception(exception.Message + "\n" + exception.StackTrace);
            }
        }
        
        #endregion Private Methods
    }
}