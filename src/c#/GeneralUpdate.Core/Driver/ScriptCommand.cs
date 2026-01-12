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
            if (!System.IO.File.Exists(scriptPath))
                throw new ApplicationException($"Script file not found: {scriptPath}");
            
            var processStartInfo = new ProcessStartInfo
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = scriptPath,
                UseShellExecute = true,
                Verb = "runas"
            };

            var process = new Process();
            try
            {
                process.StartInfo = processStartInfo;
                process.Start();
                process.WaitForExit();

                if (process.ExitCode != 0)
                    throw new ApplicationException($"Script execution failed for '{scriptPath}' with exit code: {process.ExitCode}");
                
                GeneralTracer.Info($"Script executed successfully: {scriptPath}");
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
