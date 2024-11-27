using System.Collections.Generic;
using GeneralUpdate.Bowl.Strategys;

namespace GeneralUpdate.Bowl.Internal;

internal class Crash
{
    public MonitorParameter Parameter { get; set; }
    
    public List<string> ProcdumpOutPutLines { get; set; }
}