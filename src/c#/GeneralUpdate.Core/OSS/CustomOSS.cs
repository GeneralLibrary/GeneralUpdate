using GeneralUpdate.Core.Domain.Entity;
using GeneralUpdate.Core.Download;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.OSS
{
    public class CustomOSS : IOSS
    {
        public Task<string> Download()
        {
            throw new NotImplementedException();
        }

        public void SetParmeter(string url)
        {
            throw new NotImplementedException();
        }
    }
}
