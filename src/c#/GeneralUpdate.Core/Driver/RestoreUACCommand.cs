namespace GeneralUpdate.Core.Driver
{
    /// <summary>
    /// Command to restore User Account Control (UAC) via script execution.
    /// The script content is not managed by this class; only the script entry point is provided.
    /// </summary>
    public class RestoreUACCommand : ScriptCommand
    {
        public RestoreUACCommand(string scriptPath) : base(scriptPath)
        {
        }
    }
}
