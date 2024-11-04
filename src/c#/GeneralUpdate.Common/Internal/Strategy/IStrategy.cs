using System.Threading.Tasks;
using GeneralUpdate.Common.Shared.Object;

namespace GeneralUpdate.Common.Internal.Strategy
{
    /// <summary>
    /// Update the strategy, if you need to extend it, you need to inherit this interface.
    /// </summary>
    public interface IStrategy
    {
        /// <summary>
        /// Execution strategy.
        /// </summary>
        void Execute();

        /// <summary>
        /// After the update is complete.
        /// </summary>
        /// <param name="appName"></param>
        /// <param name="appType"></param>
        /// <returns></returns>
        void StartApp(string appName);
        
        /// <summary>
        /// Execution strategy.
        /// </summary>
        Task ExecuteTaskAsync();

        /// <summary>
        /// Create a strategy.
        /// </summary>
        void Create(GlobalConfigInfo parameter);
    }
}