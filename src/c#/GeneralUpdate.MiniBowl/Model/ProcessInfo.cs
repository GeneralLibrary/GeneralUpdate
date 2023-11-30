using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeneralUpdate.MiniBowl.Model
{
    public enum ProcessStatus 
    {
        Run,
        None,
        Down
    }

    internal class ProcessInfo
    {
        public string Name { get; set; }

        public int Id { get; set; }

        public ProcessStatus Status { get; set; }

        public string Path { get; set; }
    }
}
