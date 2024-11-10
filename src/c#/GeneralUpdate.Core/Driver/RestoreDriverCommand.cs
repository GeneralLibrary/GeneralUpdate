using System;
using System.Linq;
using System.Text;
using GeneralUpdate.Common.FileBasic;

namespace GeneralUpdate.Core.Driver
{
    public class RestoreDriverCommand(DriverInformation information)
    {
        public void Execute()
        {
            try
            {
                var backupFiles = GeneralFileManager.GetAllfiles(information.OutPutDirectory);
                var fileExtension = information.DriverFileExtension;
                var drivers = backupFiles.Where(x => x.FullName.EndsWith(fileExtension)).ToList();
                
                foreach (var driver in drivers)
                {
                    try
                    {
                        //Install all drivers in the specified directory, and if the installation fails, restore all the drivers in the backup directory.
                        var command = new StringBuilder("/c pnputil /add-driver ")
                            .Append(driver.FullName)
                            .Append(" /install")
                            .ToString();
                        CommandExecutor.ExecuteCommand(command);
                    }
                    catch (Exception e)
                    {
                        throw new ApplicationException($"Failed to execute install command for {driver.FullName}, error: {e.Message} !");
                    }
                }
            }
            catch
            {
                throw new ApplicationException($"Failed to execute restore command for {information.OutPutDirectory}");
            }
        }
    }
}