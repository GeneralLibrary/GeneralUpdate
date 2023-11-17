using System;
using System.Diagnostics;

namespace GeneralUpdate.Core.Driver
{
    /// <summary>
    /// When the process starts, PnPUtil is used to execute driver processing commands.
    /// </summary>
    public class CommandExecutor
    {
        public static void ExecuteCommand(string command)
        {
            /*
             *Problems may occur, including:
Permission issues: PnPUtil requires administrator rights to run. If you try to run it without the proper permissions, the backup or restore may fail.
Driver compatibility: Although the backed up drivers work properly at backup time, if the operating system is upgraded, the backed up drivers may no longer be compatible with the new operating system version.
Hardware problems: If the hardware device fails or the hardware configuration changes, the backup driver may not work properly.

To minimize these risks, the following measures are recommended:
Before doing anything, create a system restore point so that it can be restored to its previous state if something goes wrong.
Update the driver regularly to ensure that the driver is compatible with the current operating system version.
If possible, use pre-tested drivers that are proven to work.
             * 
             */
            
            var processStartInfo = new ProcessStartInfo
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = "cmd.exe",
                Arguments = command,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                Verb = "runas"
            };

            using (var process = new Process { StartInfo = processStartInfo })
            {
                process.Start();
                process.WaitForExit();

                if (process.ExitCode != 0)
                    throw new Exception("Operation failed: " + process.StandardOutput.ReadToEnd());
            }
        }
    }
}