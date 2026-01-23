using System.Collections.Generic;

namespace MyApp.Extensions.Runtime
{
    /// <summary>
    /// Represents information about a runtime environment.
    /// </summary>
    public class RuntimeEnvironmentInfo
    {
        /// <summary>
        /// Gets or sets the type of runtime.
        /// </summary>
        public RuntimeType RuntimeType { get; set; }

        /// <summary>
        /// Gets or sets the version of the runtime.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the installation path of the runtime.
        /// </summary>
        public string InstallationPath { get; set; }

        /// <summary>
        /// Gets or sets the startup parameters or arguments for the runtime.
        /// </summary>
        public Dictionary<string, string> StartupParameters { get; set; }

        /// <summary>
        /// Gets or sets the environment variables for the runtime.
        /// </summary>
        public Dictionary<string, string> EnvironmentVariables { get; set; }

        /// <summary>
        /// Gets or sets the working directory for the runtime.
        /// </summary>
        public string WorkingDirectory { get; set; }

        /// <summary>
        /// Gets or sets the maximum memory allocation for the runtime in MB.
        /// </summary>
        public int MaxMemoryMB { get; set; }

        /// <summary>
        /// Gets or sets the timeout for runtime operations in seconds.
        /// </summary>
        public int TimeoutSeconds { get; set; }
    }
}
