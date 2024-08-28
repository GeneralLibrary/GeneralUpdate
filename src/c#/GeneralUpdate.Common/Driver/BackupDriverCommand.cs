using System.IO;
using System.Text;

namespace GeneralUpdate.Common.Driver
{
    /// <summary>
    /// When the /export-driver command backs up a driver, it backs up the driver package along with all its dependencies, such as associated library files and other related files.
    /// </summary>
    public class BackupDriverCommand : IDriverCommand
    {
        private DriverInformation _information;

        public BackupDriverCommand(DriverInformation information) => _information = information;

        public void Execute()
        {
            /*
             * Back up the specified list of drives.
             */
            foreach (var driver in _information.Drivers)
            {
                //Export the backup according to the driver name.
                var outPutDirectory = Path.Combine(_information.OutPutDirectory, Path.GetFileNameWithoutExtension(driver));

                if (Directory.Exists(outPutDirectory))
                    Directory.Delete(outPutDirectory, true);

                Directory.CreateDirectory(outPutDirectory);

                /*
                 * If no test driver files are available, you can run the following command to export all installed driver files.
                 *  (1) dism /online /export-driver /destination:"D:\packet\cache\"
                 *  (2) pnputil /export-driver * D:\packet\cache
                 *
                 *  The following code example exports the specified driver to the specified directory.
                 *  pnputil /export-driver oem14.inf D:\packet\cache
                 */
                var command = new StringBuilder("/c pnputil /export-driver ")
                .Append(driver)
                .Append(' ')
                .Append(outPutDirectory)
                .ToString();

                CommandExecutor.ExecuteCommand(command);
            }
        }
    }
}