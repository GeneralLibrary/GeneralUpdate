using GeneralUpdate.Core.Utils;

namespace TestMD5
{
    public class Tests
    {
        [Test]
        public void Test1()
        {
            try
            {
                string path = "";
                var md5 = FileUtil.GetFileMD5(path);
                if (string.IsNullOrWhiteSpace(md5))
                {
                    Assert.Fail();
                }
            }
            catch (Exception ex)
            {
                Assert.Fail();
            }
            Assert.Pass();
        }
    }
}