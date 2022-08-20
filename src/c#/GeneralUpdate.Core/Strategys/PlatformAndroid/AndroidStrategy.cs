using GeneralUpdate.Core.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Text;

namespace GeneralUpdate.Core.Strategys.PlatformAndroid
{
    public class AndroidStrategy : AbstractStrategy
    {
        public override string GetPlatform()
        {
            return PlatformType.Android;
        }
    }
}
