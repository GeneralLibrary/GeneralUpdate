﻿using System.IO;
using System.Text;

namespace GeneralUpdate.Common.Driver
{
    /// <summary>
    /// Install the new driver, and if the installation fails, the backup is automatically restored.
    /// </summary>
    public class InstallDriverCommand : IDriverCommand
    {
        private DriverInformation _information;

        public InstallDriverCommand(DriverInformation information) => _information = information;

        public void Execute()
        {
            try
            {
                foreach (var driver in _information.Drivers)
                {
                    /*
                     * 1.It is best to ensure that the installed file is OEM INF, otherwise PnPUtil may indicate that non-OEM INF cannot perform the current operation.
                     *
                     * 2.Before installation, you need to delete the previously installed driver, otherwise PnPUtil will prompt 259 to exit the code.
                     * (On Windows, an ExitCode value of 259 (STILL_ACTIVE) means that the process is still running)
                     * If you do not remove the previous installation 259 prompt will give you a misleading impression of what is running.
                     */
                    var path = Path.Combine(_information.InstallDirectory, Path.GetFileNameWithoutExtension(driver), driver);
                    var command = new StringBuilder("/c pnputil /add-driver ")
                        .Append(path)
                        .Append(" /install")
                        .ToString();
                    CommandExecutor.ExecuteCommand(command);
                }
            }
            catch (System.Exception ex)
            {
                //restore all the drivers in the backup directory.
                new RestoreDriverCommand(_information).Execute();
                throw new System.Exception($"Failed to execute install command for {_information.InstallDirectory}", ex);
            }
        }
    }
}