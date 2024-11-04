using GeneralUpdate.Core.Driver;

namespace GeneralUpdate.Upgrad
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var fileExtension = ".inf";
            var outPutPath = @"D:\Temp\";
            var driversPath = @"D:\Temp\";
            
            var information = new DriverInformation.Builder()
                .SetDriverFileExtension(fileExtension)
                .SetOutPutDirectory(outPutPath)
                .SetDrivers(driversPath, fileExtension)
                .Build();

            var processor = new DriverProcessor();
            processor.AddCommand(new BackupDriverCommand(information));
            processor.AddCommand(new InstallDriverCommand(information));
            processor.ProcessCommands();

            Console.Read();
        }
    }
}