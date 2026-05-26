using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.Strategy
{
    /// <summary>
    /// Update the strategy, if you need to extend it, you need to inherit this interface.
    /// </summary>
    public interface IStrategy
    {
        /// <summary>
        /// Execution strategy.
        /// </summary>
        Task ExecuteAsync();

        /// <summary>
        /// After the update is complete.
        /// </summary>
        Task StartAppAsync();

        /// <summary>
        /// Create a strategy.
        /// </summary>
        void Create(GlobalConfigInfo parameter);
    }
}