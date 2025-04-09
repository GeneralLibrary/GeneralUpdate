using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using GeneralUpdate.Common.Internal.Bootstrap;
using GeneralUpdate.Common.Internal.JsonContext;
using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Core.Strategys;

namespace GeneralUpdate.Core
{
    public sealed class GeneralUpdateOSS
    {
        private static Func<string, bool>? _beforeFunc;
        private static Action<string>? _afterFunc;
        
        private GeneralUpdateOSS() { }

        #region Public Methods

        /// <summary>
        /// Starting an OSS update for windows,Linux,mac platform.
        /// </summary>
        /// <param name="beforeFunc">Inject a custom pre-processing method, which will be executed before updating. whose parameters use the Extend.</param>
        /// <param name="afterFunc">Injects a post-processing method whose parameters use the Extend2</param>
        public static async Task Start(Func<string, bool> beforeFunc, Action<string> afterFunc)
        {
            _beforeFunc = beforeFunc;
            _afterFunc = afterFunc;
            await BaseStart();
        }
        
        /// <summary>
        /// Starting an OSS update for windows,Linux,mac platform.
        /// </summary>
        /// <returns></returns>
        public static async Task Start() => await BaseStart();

        #endregion Public Methods

        #region Private Methods
        
        /// <summary>
        /// The underlying update method.
        /// </summary>
        private static async Task BaseStart()
        {
            try
            {
                var json = Environments.GetEnvironmentVariable("GlobalConfigInfoOSS");
                if (string.IsNullOrWhiteSpace(json))
                    return;

                var parameter = JsonSerializer.Deserialize<GlobalConfigInfoOSS>(json, GlobalConfigInfoOSSJsonContext.Default.GlobalConfigInfoOSS);
                var result = _beforeFunc?.Invoke(parameter?.Extend);
                if (_beforeFunc is not null)
                {
                    if (result == false)
                        return;
                }
                
                var strategy = new OSSStrategy();
                strategy.Create(parameter);
                await strategy.ExecuteAsync();
                _afterFunc?.Invoke(parameter?.Extend2);
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception);
                throw new Exception(exception.Message + "\n" + exception.StackTrace);
            }
        }
        
        #endregion Private Methods
    }
}