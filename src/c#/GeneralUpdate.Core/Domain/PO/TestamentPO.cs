using System;

namespace GeneralUpdate.Core.Domain.PO
{
    public class TestamentPO
    {
        public string UUID { get; set; }

        public string DumpPath { get; set; }

        public DateTime CreateTime { get; set; }

        public Exception Exception { get; set; }

        public VersionPO Version { get; set; }
    }
}
