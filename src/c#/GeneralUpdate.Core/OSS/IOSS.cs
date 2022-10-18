using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.OSS
{
    public interface IOSS
    {
        void SetParmeter(string url,string targetPath);

        Task Download();
    }
}
