namespace GeneralUpdate.Bowl.Strategys;

public class MonitorParameter
{
    public MonitorParameter() { }
    
    public string TargetPath { get; set; }
    
    public string FailDirectory { get; set; }
    
    public string BackupDirectory { get; set; }
    
    public string ProcessNameOrId { get; set; }
 
    public string DumpFileName { get; set; }
    
    public string FailFileName { get; set; }
    
    internal string InnerArguments { get; set; }

    internal string InnerApp { get; set; }
    
    /// <summary>
    /// Upgrade: upgrade mode. This mode is primarily used in conjunction with GeneralUpdate for internal use. Please do not modify it arbitrarily when the default mode is activated.
    /// Normal: Normal mode,This mode can be used independently to monitor a single program. If the program crashes, it will export the crash information.
    /// </summary>
    public string WorkModel { get; set; } = "Upgrade";
    
    public string ExtendedField { get; set; }
}