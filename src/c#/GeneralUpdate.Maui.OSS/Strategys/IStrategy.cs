using System.Threading.Tasks;

namespace GeneralUpdate.Maui.OSS.Strategys
{
    public interface IStrategy
    {
        /// <summary>
        /// Execution strategy.
        /// </summary>
        Task Excute();

        /// <summary>
        /// Create a policy.
        /// </summary>
        /// <param name="file">Abstraction for updating package information.</param>
        void Create<T>(T parameter) where T : class;
    }
}