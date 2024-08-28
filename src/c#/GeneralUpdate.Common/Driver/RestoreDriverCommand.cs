using System.IO;
using System.Text;

namespace GeneralUpdate.Common.Driver
{
    public class RestoreDriverCommand : IDriverCommand
    {
        private DriverInformation _information;

        public RestoreDriverCommand(DriverInformation information) => _information = information;

        public void Execute()
        {
            foreach (var driver in _information.Drivers)
            {
                //Install all drivers in the specified directory, and if the installation fails, restore all the drivers in the backup directory.
                var command = new StringBuilder("/c pnputil /add-driver ")
                    .Append(Path.Combine(_information.OutPutDirectory, Path.GetFileNameWithoutExtension(driver), driver))
                    .Append(" /install")
                    .ToString();
                CommandExecutor.ExecuteCommand(command);
            }
        }
    }
}