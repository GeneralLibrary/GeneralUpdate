using System;
using System.IO;
using System.Text;

namespace GeneralUpdate.Core.Driver
{
    /// <summary>
    /// Install the new driver, and if the installation fails, the backup is automatically restored.
    /// </summary>
    public class InstallDriverCommand(DriverInformation information) : DriverCommand
    {
        public override void Execute()
        {
            foreach (var driver in information.Drivers)
            {
                try
                {
                    /*
                     * 1.It is best to ensure that the installed file is OEM INF, otherwise PnPUtil may indicate that non-OEM INF cannot perform the current operation.
                     *
                     * 2.Before installation, you need to delete the previously installed driver, otherwise PnPUtil will prompt 259 to exit the code.
                     * (On Windows, an ExitCode value of 259 (STILL_ACTIVE) means that the process is still running)
                     * If you do not remove the previous installation 259 prompt will give you a misleading impression of what is running.
                     */
                    var path = Path.Combine(information.DriverDirectory, driver.OriginalName);
                    var command = new StringBuilder("/c pnputil /add-driver ")
                        .Append(path)
                        .Append(" /install")
                        .ToString();
                    CommandExecutor.ExecuteCommand(command);
                }
                catch (Exception ex)
                {
                    //restore all the drivers in the backup directory.
                    new RestoreDriverCommand(information).Execute();
                    throw new ApplicationException($"Failed to execute driver command: {ex.Message}, details: {ex} !");
                }
            }
        }
    }
}