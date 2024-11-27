using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using GeneralUpdate.Common.FileBasic;

namespace GeneralUpdate.Core.Driver
{
    /// <summary>
    /// When the /export-driver command backs up a driver, it backs up the driver package along with all its dependencies, such as associated library files and other related files.
    /// </summary>
    public class BackupDriverCommand(DriverInformation information) : DriverCommand
    {
        private readonly string _driverExtension = $"*{information.DriverFileExtension}";

        public override void Execute()
        {
            var uninstalledDrivers = Directory.GetFiles(information.DriverDirectory, _driverExtension, SearchOption.AllDirectories).ToList();
            var installedDrivers = GetInstalledDrivers(information.FieldMappings);
            var tempDrivers = installedDrivers.Where(a => uninstalledDrivers.Any(b => string.Equals(a.OriginalName, Path.GetFileName(b)))).ToList();
            information.Drivers = tempDrivers;
            
            //Export the backup according to the driver name.
            if (Directory.Exists(information.OutPutDirectory))
            {
                StorageManager.DeleteDirectory(information.OutPutDirectory);
            }
            
            Directory.CreateDirectory(information.OutPutDirectory);
            
            /*
             * Back up the specified list of drives.
             */
            foreach (var driver in tempDrivers)
            {
                /*
                 * If no test driver files are available, you can run the following command to export all installed driver files.
                 *  (1) dism /online /export-driver /destination:"D:\packet\cache\"
                 *  (2) pnputil /export-driver * D:\packet\cache
                 *
                 *  The following code example exports the specified driver to the specified directory.
                 *  pnputil /export-driver oemXX.inf D:\packet\cache
                 */
                var path = Path.Combine(information.OutPutDirectory, driver.PublishedName);
                var command = new StringBuilder("/c pnputil /export-driver ")
                .Append(driver.PublishedName)
                .Append(' ')
                .Append(path)
                .ToString();
                
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                
                CommandExecutor.ExecuteCommand(command);
            }
        }

        private IEnumerable<DriverInfo> GetInstalledDrivers(Dictionary<string, string> fieldMappings)
        {
            var drivers = new List<DriverInfo>();
            var process = new Process();
            process.StartInfo.FileName = "pnputil";
            process.StartInfo.Arguments = "/enum-drivers";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            var lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        
            DriverInfo currentDriver = null;
            foreach (var line in lines)
            {
                if (line.StartsWith(fieldMappings["PublishedName"]))
                {
                    if (currentDriver != null)
                    {
                        drivers.Add(currentDriver);
                    }
                    currentDriver = new ();
                    currentDriver.PublishedName = line.Split(new[] { ':' }, 2)[1].Trim();
                }
                else if (line.StartsWith(fieldMappings["OriginalName"]) && currentDriver != null)
                {
                    currentDriver.OriginalName = line.Split(new[] { ':' }, 2)[1].Trim();
                }
                else if (line.StartsWith(fieldMappings["Provider"]) && currentDriver != null)
                {
                    currentDriver.Provider = line.Split(new[] { ':' }, 2)[1].Trim();
                }
                else if (line.StartsWith(fieldMappings["ClassName"]) && currentDriver != null)
                {
                    currentDriver.ClassName = line.Split(new[] { ':' }, 2)[1].Trim();
                }
                else if (line.StartsWith(fieldMappings["ClassGUID"]) && currentDriver != null)
                {
                    currentDriver.ClassGUID = line.Split(new[] { ':' }, 2)[1].Trim();
                }
                else if (line.StartsWith(fieldMappings["Version"]) && currentDriver != null)
                {
                    currentDriver.Version = line.Split(new[] { ':' }, 2)[1].Trim();
                }
                else if (line.StartsWith(fieldMappings["Signer"]) && currentDriver != null)
                {
                    currentDriver.Signer = line.Split(new[] { ':' }, 2)[1].Trim();
                }
            }

            if (currentDriver != null)
            {
                drivers.Add(currentDriver);
            }

            return drivers;
        }
    }
}