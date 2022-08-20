using GeneralUpdate.Core.Domain.Enum;
using System;
using System.Collections.Generic;
using System.Text;

namespace GeneralUpdate.Core.Strategys.PlatformLinux
{
    public class LinuxStrategy : AbstractStrategy
    {
        public override string GetPlatform()
        {
            return PlatformType.Linux;
        }
    }
}
