using GeneralUpdate.Core.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Text;

namespace GeneralUpdate.Core.Strategys.PlatformAndroid
{
    public class AndroidStrategy : AbstractStrategy
    {
        protected override string GetPlatform()
        {
            return PlatformType.Android;
        }
    }
}
