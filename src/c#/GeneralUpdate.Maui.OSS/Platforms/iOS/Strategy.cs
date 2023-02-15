using GeneralUpdate.Maui.OSS.Strategys;

namespace GeneralUpdate.Maui.OSS
{
    // All the code in this file is only included on iOS.
    public class Strategy : AbstractStrategy
    {
        public override void Create<T>(T parameter)
        {
            throw new NotImplementedException();
        }

        public override Task Excute()
        {
            throw new NotImplementedException();
        }
    }
}