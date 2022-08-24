using System.Text;

namespace TestMD5
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

        //#region MD5

        //private void Button_Click_1(object sender, RoutedEventArgs e)
        //{
        //    try
        //    {
        //        OpenFileDialog openFile = new OpenFileDialog();
        //        bool? isOpen = openFile.ShowDialog(this);
        //        if (isOpen != null && isOpen.Value)
        //        {
        //            var name = openFile.FileName;
        //            var md5 = GetFileMD5(name);
        //            TxtMD5.Text = md5;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show($"Failed to get MD5: {ex.Message} ");
        //    }
        //}

        //private string GetFileMD5(string fileName)
        //{
        //    try
        //    {
        //        var file = new FileStream(fileName, FileMode.Open);
        //        var md5 = new MD5CryptoServiceProvider();
        //        var retVal = md5.ComputeHash(file);
        //        file.Close();
        //        var sb = new StringBuilder();
        //        for (var i = 0; i < retVal.Length; i++)
        //        {
        //            sb.Append(retVal[i].ToString("x2"));
        //        }
        //        return sb.ToString();
        //    }
        //    catch (Exception)
        //    {
        //        return string.Empty;
        //    }
        //}

        //#endregion MD5
    }
}