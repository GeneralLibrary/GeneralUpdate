using System;
using System.Diagnostics;
using GeneralUpdate.Common.Shared;

namespace GeneralUpdate.Core.Driver
{
    /// <summary>
    /// Base class for executing script commands.
    /// </summary>
    public abstract class ScriptCommand : DriverCommand
    {
        protected string ScriptPath { get; }

        protected ScriptCommand(string scriptPath)
        {
            if (string.IsNullOrWhiteSpace(scriptPath))
                throw new ArgumentNullException(nameof(scriptPath), "Script path cannot be null or empty.");
            
            ScriptPath = scriptPath;
        }

        public override void Execute()
        {
            ExecuteScript(ScriptPath);
        }

        /// <summary>
        /// Execute a script file.
        /// </summary>
        /// <param name="scriptPath">Path to the script file to execute.</param>
        protected virtual void ExecuteScript(string scriptPath)
        {
            var processStartInfo = new ProcessStartInfo
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = scriptPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Verb = "runas"
            };

            var process = new Process();
            try
            {
                process.StartInfo = processStartInfo;
                process.Start();
                process.WaitForExit();

                var output = process.StandardOutput.ReadToEnd();
                GeneralTracer.Info($"Script execution output: {output}");

                var error = process.StandardError.ReadToEnd();
                if (!string.IsNullOrEmpty(error))
                {
                    GeneralTracer.Error($"Script execution error: {error}");
                }

                if (process.ExitCode != 0)
                    throw new ApplicationException($"Script execution failed with exit code: {process.ExitCode}");
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
