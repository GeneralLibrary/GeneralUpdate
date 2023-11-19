using System.IO;
using System.Text;

namespace GeneralUpdate.Core.Driver
{
    
    /// <summary>
    /// When the /export-driver command backs up a driver, it backs up the driver package along with all its dependencies, such as associated library files and other related files.
    /// </summary>
    public class BackupDriverCommand : IDriverCommand
    {
        private DriverInformation _information;

        public BackupDriverCommand(DriverInformation information)
        {
            _information = information;
        }
        
        public void Execute()
        {
            /*
             * Back up the specified list of drives.
             */
            foreach (var driverName in _information.DriverNames)
            {
                var command = new StringBuilder("/c dism /online /export-driver /destination:\"")
                .Append(Path.Combine(_information.OutPutDirectory, driverName))
                .Append("\"")
                .ToString();
                CommandExecutor.ExecuteCommand(command);
            }
        }
    }
}