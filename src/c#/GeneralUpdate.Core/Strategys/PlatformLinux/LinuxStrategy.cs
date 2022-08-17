using GeneralUpdate.Core.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Text;

namespace GeneralUpdate.Core.Strategys.PlatformLinux
{
    public class StrategyLinux : AbstractStrategy
    {
        protected override string GetPlatform()
        {
            return PlatformType.Linux;
        }
    }
}
