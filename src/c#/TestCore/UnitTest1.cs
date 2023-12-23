using GeneralUpdate.Core.Driver;

namespace TestCore
{
    public class Tests
    {
        [Test]
        public void Test1()
        {
            string installDir = "D:\\packet\\patch";
            string outPutDir = "D:\\packet\\cache";
            string name = "netrasa.inf";

            var information = new DriverInformation.Builder()
                                         .SetInstallDirectory(installDir)
                                         .SetOutPutDirectory(outPutDir)
                                         .SetDriverNames(new List<string> { name })
                                         .Build();

            Assert.IsNotNull(information);

            var processor = new DriverProcessor();
            processor.AddCommand(new BackupDriverCommand(information));
            processor.AddCommand(new DeleteDriverCommand(information));
            processor.AddCommand(new InstallDriverCommand(information));
            processor.ProcessCommands();
        }
    }
}