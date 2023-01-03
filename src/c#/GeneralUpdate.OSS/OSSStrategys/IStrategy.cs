namespace GeneralUpdate.OSS.OSSStrategys
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
        void Create(params string[] arguments);
    }
}