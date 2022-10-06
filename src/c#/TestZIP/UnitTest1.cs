using System.Diagnostics;

namespace TestZIP
{
    public class Tests
    {

        [Test]
        public void Test1()
        {
            Assert.Pass();
        }

        //#region GeneralUpdate Zip

        ///// <summary>
        ///// Create Zip
        ///// </summary>
        ///// <param name="sender"></param>
        ///// <param name="e"></param>
        //private void BtnCreateZip_Click(object sender, RoutedEventArgs e)
        //{
        //    try
        //    {
        //        var factory = new GeneralZipFactory();
        //        factory.CompressProgress += OnCompressProgress;
        //        //Compress all files in this path£ºD:\Updatetest_hub\Run_app £¬ D:\Updatetest_hub
        //        factory.CreatefOperate(GetOperationType(), TxtZipPath.Text, TxtUnZipPath.Text).
        //            CreatZip();
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show($"CreatZip error : {ex.Message} ");
        //    }
        //}

        ///// <summary>
        ///// UnZip
        ///// </summary>
        ///// <param name="sender"></param>
        ///// <param name="e"></param>
        //private void BtnUnZip_Click(object sender, RoutedEventArgs e)
        //{
        //    try
        //    {
        //        var factory = new GeneralZipFactory();
        //        factory.UnZipProgress += OnUnZipProgress;
        //        factory.Completed += OnCompleted;
        //        //D:\Updatetest_hub\Run_app\1.zip , D:\Updatetest_hub
        //        factory.CreatefOperate(GetOperationType(), TxtZipPath.Text, TxtUnZipPath.Text, true).
        //            UnZip();
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show($"UnZip error : {ex.Message} ");
        //    }
        //}

        //private void OnCompleted(object sender, BaseCompleteEventArgs e)
        //{
        //    Debug.WriteLine($"IsCompleted {e.IsCompleted}.");
        //}

        //private OperationType GetOperationType()
        //{
        //    OperationType operationType = 0;
        //    var item = CmbxZipFormat.SelectedItem as ComboBoxItem;
        //    switch (item.Content)
        //    {
        //        case "ZIP":
        //            operationType = OperationType.GZip;
        //            break;

        //        case "7z":
        //            operationType = OperationType.G7z;
        //            break;
        //    }
        //    return operationType;
        //}

        //private void OnCompressProgress(object sender, BaseCompressProgressEventArgs e)
        //{ Debug.WriteLine($"CompressProgress - name:{e.Name}, count:{e.Count}, index:{e.Index}, size:{e.Size}."); }

        //private void OnUnZipProgress(object sender, BaseUnZipProgressEventArgs e)
        //{ Debug.WriteLine($"UnZipProgress - name:{e.Name}, count:{e.Count}, index:{e.Index}, size:{e.Size}."); }

        //#endregion GeneralUpdate Zip
    }
}