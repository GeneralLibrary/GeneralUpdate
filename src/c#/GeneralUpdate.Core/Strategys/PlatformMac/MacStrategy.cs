using GeneralUpdate.Core.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Text;

namespace GeneralUpdate.Core.Strategys.PlatformMac
{
    public class MacStrategy : AbstractStrategy
    {
        protected override string GetPlatform()
        {
            return PlatformType.Mac;
        }
    }
}
