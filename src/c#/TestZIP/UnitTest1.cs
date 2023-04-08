using GeneralUpdate.Zip;
using GeneralUpdate.Zip.Factory;

namespace TestZIP
{
    public class Tests
    {
        [Test]
        public void CreatZip()
        {
            try
            {
                string sourcePath = "D:\\Updatetest_hub\\Run_app";
                string destinationPath = "D:\\Updatetest_hub";
                string name = "testpacket.zip";
                var factory = new GeneralZipFactory();
                factory.CompressProgress += (a, e) =>
                {
                    Console.WriteLine($"fileName:{e.Name},fileSize:{e.Size},fileIndex:{e.Index},filePath:{e.Path},fileCount:{e.Count}");
                };
                factory.Completed += (a, e) =>
                {
                    Console.WriteLine($"IsCompleted:{e.IsCompleted}");
                };
                factory.CreateOperate(OperationType.GZip, name, sourcePath, destinationPath, false, System.Text.Encoding.Default).
                    CreateZip();
            }
            catch
            {
                Assert.Fail();
            }
            Assert.Pass();
        }

        [Test]
        public void UnZip()
        {
            try
            {
                string sourcePath = "D:\\Updatetest_hub\\Run_app\\1.zip";
                string destinationPath = "D:\\Updatetest_hub";
                string name = "testpacket.zip";
                var factory = new GeneralZipFactory();
                factory.UnZipProgress += (a, e) =>
                {
                    Console.WriteLine($"fileName:{e.Name},fileSize:{e.Size},fileIndex:{e.Index},filePath:{e.Path},fileCount:{e.Count}");
                };
                factory.Completed += (a, e) =>
                {
                    Console.WriteLine($"IsCompleted:{e.IsCompleted}");
                };
                factory.CreateOperate(OperationType.GZip, name, sourcePath, destinationPath, false, System.Text.Encoding.Default).
                    UnZip();
            }
            catch (Exception ex)
            {
            }
        }
    }
}