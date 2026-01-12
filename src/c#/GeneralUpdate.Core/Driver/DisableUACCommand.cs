namespace GeneralUpdate.Core.Driver
{
    /// <summary>
    /// Command to disable User Account Control (UAC) via script execution.
    /// The script content is not managed by this class; only the script entry point is provided.
    /// </summary>
    public class DisableUACCommand : ScriptCommand
    {
        public DisableUACCommand(string scriptPath) : base(scriptPath)
        {
        }
    }
}
