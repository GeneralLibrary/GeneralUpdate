using System;
using System.Collections.Generic;
using System.Text;

namespace GeneralUpdate.Core.Domain.DO
{
    public class VersionConfigDO
    {
        public string Url { get; set; }

        public string MD5 { get; set; }

        public string PacketName { get; set; }

        public string Version { get; set; }
    }
}
