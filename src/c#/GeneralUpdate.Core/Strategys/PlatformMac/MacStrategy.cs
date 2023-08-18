using GeneralUpdate.Core.Domain.Enum;

namespace GeneralUpdate.Core.Strategys.PlatformMac
{
    public class MacStrategy : AbstractStrategy
    {
        public override string GetPlatform() => PlatformType.Mac;

        public override void Create<T>(T parameter)
        {
            base.Create(parameter);
        }

        public override void Execute()
        {
            base.Execute();
        }

        public override bool StartApp(string appName, int appType)
        {
            return base.StartApp(appName, appType);
        }
    }
}