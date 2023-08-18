using GeneralUpdate.Core.Domain.Enum;

namespace GeneralUpdate.Core.Strategys.PlatformLinux
{
    public class LinuxStrategy : AbstractStrategy
    {
        public override string GetPlatform() => PlatformType.Linux;

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