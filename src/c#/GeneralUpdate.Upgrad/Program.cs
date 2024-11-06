using GeneralUpdate.Core.Driver;

namespace GeneralUpdate.Upgrad
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var fileExtension = ".inf";
            var outPutPath = @"D:\drivers\";
            var driversPath = @"D:\driverslocal\";
            
            var fieldMappingsCN = new Dictionary<string, string>
            {
                { "PublishedName", "发布名称" },
                { "OriginalName", "原始名称" },
                { "Provider", "提供程序名称" },
                { "ClassName", "类名" },
                { "ClassGUID", "类 GUID" },
                { "Version", "驱动程序版本" },
                { "Signer", "签名者姓名" }
            };
            
            var fieldMappingsEN = new Dictionary<string, string>
            {
                { "PublishedName", "Driver" },
                { "OriginalName", "OriginalFileName" },
                { "Provider", "ProviderName" },
                { "ClassName", "ClassName" },
                { "Version", "Version" }
            };
            
            var information = new DriverInformation.Builder()
                .SetDriverFileExtension(fileExtension)
                .SetOutPutDirectory(outPutPath)
                .SetDriverDirectory(driversPath)
                .SetFieldMappings(fieldMappingsCN)
                .Build();

            var processor = new DriverProcessor();
            processor.AddCommand(new BackupDriverCommand(information));
            processor.AddCommand(new DeleteDriverCommand(information));
            processor.AddCommand(new InstallDriverCommand(information));
            processor.ProcessCommands();

            Console.Read();
        }
    }
}