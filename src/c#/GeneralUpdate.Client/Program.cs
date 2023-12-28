namespace GeneralUpdate.Client
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Task.Run(async() => 
            {
                MySample sample = new MySample();
                //await sample.TestDifferentialClean();
                //await sample.TestDifferentialDirty();
            });
            Console.Read();
        }
    }
}
