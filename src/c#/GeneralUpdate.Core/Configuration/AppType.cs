namespace GeneralUpdate.Core.Configuration
{
    public class AppType
    {
        /// <summary>
        /// main program
        /// </summary>
        public const int ClientApp = 1;

        /// <summary>
        /// upgrade program.
        /// </summary>
        public const int UpgradeApp = 2;

        /// <summary>
        /// OSS (Object Storage Service) update mode.
        /// Downloads packages from cloud storage without a dedicated update server.
        /// </summary>
        public const int OSSApp = 3;
    }
}
