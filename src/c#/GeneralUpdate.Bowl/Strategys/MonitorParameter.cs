namespace GeneralUpdate.Bowl.Strategys;

public class MonitorParameter
{
    public string Target { get; set; }

    public string Source { get; set; }
    
    public string ProcessNameOrId { get; set; }
    
    public string DumpPath { get; set; }
    
    public string DumpFileName { get; set; }
    
    public string Arguments { get; set; }
    
    internal string InnerArguments => $"-e -ma {ProcessNameOrId} {DumpPath}";

    internal string InnerAppName  { get; set; } 

    public bool Verify()
    {
        return string.IsNullOrEmpty(Target) &&
               string.IsNullOrEmpty(Source) &&
               string.IsNullOrEmpty(ProcessNameOrId) &&
               string.IsNullOrEmpty(DumpPath) &&
               string.IsNullOrEmpty(DumpFileName) &&
               string.IsNullOrEmpty(Arguments);
    }
}