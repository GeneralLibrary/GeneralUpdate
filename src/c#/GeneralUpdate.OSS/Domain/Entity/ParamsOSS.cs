using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeneralUpdate.OSS.Domain.Entity
{
    public class ParamsOSS : ParamsWindows
    {
        public ParamsOSS(string url, string appName, string currentVersion, string versionFileName) : base(url, appName, currentVersion, versionFileName)
        {
        }
    }
}
