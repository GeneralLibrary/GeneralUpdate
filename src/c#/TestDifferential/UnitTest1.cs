using GeneralUpdate.Differential;
using GeneralUpdate.Differential.Config;

namespace TestDifferential
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void TestDrity()
        {
            Task.Run(async () =>
            {
                try
                {
                    var path1 = @"F:\temp\source";
                    var path3 = @"F:\temp\patchs";
                    await DifferentialCore.Instance.Drity(path1, path3);
                }
                catch (Exception)
                {
                    Assert.Fail();
                }
                Assert.Pass();
            });
        }

        [Test]
        public void TestClean()
        {
            Task.Run(async () =>
            {
                try
                {
                    var path1 = @"F:\temp\source";
                    var path2 = @"F:\temp\target";
                    var path3 = @"F:\temp\patchs";
                    await DifferentialCore.Instance.Clean(path1, path2, path3);
                }
                catch (Exception)
                {
                    Assert.Fail();
                }
                Assert.Pass();
            });
        }

        //[Test]
        //public void TestScan()
        //{
        //    Task.Run(async () =>
        //    {
        //        try
        //        {
        //            var path1 = @"F:\temp\source";
        //            var path2 = @"F:\temp\target";
        //            await ConfigFactory.Instance.Scan(path1, path2);
        //        }
        //        catch (Exception)
        //        {
        //            Assert.Fail();
        //        }
        //        Assert.Pass();
        //    });
        //}

        //[Test]
        //public void TestDeploy()
        //{
        //    Task.Run(async () =>
        //    {
        //        try
        //        {
        //            await ConfigFactory.Instance.Deploy();
        //        }
        //        catch (Exception)
        //        {
        //            Assert.Fail();
        //        }
        //        Assert.Pass();
        //    });
        //}
    }
}