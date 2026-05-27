using System.Threading.Tasks;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Hooks;
using IUpdateReporter = GeneralUpdate.Core.Download.Reporting.IUpdateReporter;

namespace GeneralUpdate.Core.Strategy
{
    /// <summary>
    /// Update the strategy, if you need to extend it, you need to inherit this interface.
    /// </summary>
    public interface IStrategy
    {
        /// <summary>
        /// Lifecycle hooks for pre/post update callbacks.
        /// </summary>
        IUpdateHooks Hooks { get; set; }

        /// <summary>
        /// Update status reporter.
        /// </summary>
        IUpdateReporter Reporter { get; set; }

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