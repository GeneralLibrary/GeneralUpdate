using GeneralUpdate.Core.Domain.Enum;

namespace GeneralUpdate.Core.Strategys.PlatformMac
{
    public class MacStrategy : AbstractStrategy
    {
        public override string GetPlatform() => PlatformType.Mac;
    }
}