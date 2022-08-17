using GeneralUpdate.Core.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Text;

namespace GeneralUpdate.Core.Strategys.PlatformiOS
{
    public class iOSStrategy : AbstractStrategy
    {
        protected override string GetPlatform()
        {
            return PlatformType.iOS;
        }
    }
}
