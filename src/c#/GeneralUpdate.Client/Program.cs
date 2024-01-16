using GeneralUpdate.Core.HashAlgorithms;

namespace GeneralUpdate.Client
{
    internal class Program
    {
        static void Main(string[] args)
        {
            MySample sample = new MySample();
            sample.TestWillMessage();
            //Task.Run(async() => 
            //{
            //    //415eed05eb310f480d1e4d15516fa00e484ddb9f416908b217f17b782ded2030
            //    //var zip1 = @"D:\github_project\WpfClient\WebApi\UpdateFiles\WpfClient_1_24.1.5.1218.zip";
            //    //94bd3d806d39cd1b8813298ec0637c7f377658e766845a06cc50917306cb4ad9
            //    //var zip2 = @"D:\github_project\WpfClient\WebApi\UpdateFiles\WpfClient_1_24.1.5.1224.zip";

            //    //var hashAlgorithm = new Sha256HashAlgorithm();
            //    //var hashSha256 = hashAlgorithm.ComputeHash(zip1);
            //    //var hashSha2561 = hashAlgorithm.ComputeHash(zip2);

            //    MySample sample = new MySample();
            //    //await sample.TestDifferentialClean();
            //    //await sample.TestDifferentialDirty();
            //    await sample.Upgrade();
            //});
            Console.Read();
        }
    }
}
