using System;
using System.Text;

namespace GeneralUpdate.Core.Driver
{
    public class InstallDriverCommand : IDriverCommand
    {
        private DriverInformation _information;

        public InstallDriverCommand(DriverInformation information)
        {
            _information = information;
        }

        public void Execute()
        {
            try
            {
                //Install all drivers in the specified directory, and if the installation fails, restore all the drivers in the backup directory.
                var command = new StringBuilder("/c pnputil /add-driver \"")
                    .Append(_information.InstallDirectory)
                    .Append("\"")
                    .ToString();
                CommandExecutor.ExecuteCommand(command);
            }
            catch (Exception ex)
            {
                new RestoreDriverCommand(_information).Execute();
                throw new Exception($"Failed to execute install command for {_information.InstallDirectory}", ex);
            }
        }
    }
}