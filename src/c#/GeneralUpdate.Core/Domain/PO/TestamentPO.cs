using System;

namespace GeneralUpdate.Core.Domain.PO
{
    public class TestamentPO
    {
        public TestamentPO() 
        {
            UUID = Guid.NewGuid().ToString();
            CreateTime = DateTime.Now;
        }

        public string UUID { get; private set; }

        public string DumpPath { get; set; }

        public DateTime CreateTime { get; private set; }

        public Exception Exception { get; set; }

        public VersionPO Version { get; set; }
    }
}
