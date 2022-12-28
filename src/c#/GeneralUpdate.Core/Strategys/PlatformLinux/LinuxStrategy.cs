using GeneralUpdate.Core.Domain.Enum;

namespace GeneralUpdate.Core.Strategys.PlatformLinux
{
    public class LinuxStrategy : AbstractStrategy
    {
        public override string GetPlatform() => PlatformType.Linux;
    }
}