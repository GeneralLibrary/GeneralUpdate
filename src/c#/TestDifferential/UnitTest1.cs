namespace TestDifferential
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Test1()
        {
            Assert.Pass();
        }

        //private void BtnClean_Click(object sender, RoutedEventArgs e)
        //{
        //    Task.Run(async () =>
        //    {
        //        var path1 = @"D:\TestCode\compare\source";
        //        var path2 = @"D:\TestCode\compare\target";
        //        var path3 = @"D:\TestCode\compare\patchs";
        //        await DifferentialCore.Instance.Clean(path1, path2, path3);
        //    });
        //}

        //private void BtnDrity_Click(object sender, RoutedEventArgs e)
        //{
        //    Task.Run(async () =>
        //    {
        //        var path1 = @"D:\TestCode\compare\source";
        //        //var path2 = @"D:\TestCode\compare\target";
        //        var path3 = @"D:\TestCode\compare\patchs";
        //        await DifferentialCore.Instance.Drity(path1, path3);
        //    });
        //}

        //private void BtnScan_Click(object sender, RoutedEventArgs e)
        //{
        //    Task.Run(async () =>
        //    {
        //        var path1 = @"D:\TestCode\compare\source";
        //        var path2 = @"D:\TestCode\compare\target";
        //        await ConfigFactory.Instance.Scan(path1, path2);
        //    });
        //}

        //private void BtnDeploy_Click(object sender, RoutedEventArgs e)
        //{
        //    Task.Run(async () =>
        //    {
        //        await ConfigFactory.Instance.Deploy();
        //    });
        //}
    }
}