using System.Collections.Generic;
using System.Threading.Tasks;
using GeneralUpdate.Common.Internal.Pipeline;
using GeneralUpdate.Core.Driver;

namespace GeneralUpdate.Core.Pipeline;

/// <summary>
/// Driver update.
/// Use for Windows Vista/Windows 7/Windows 8/Windows 8.1/Windows 10/Windows 11/Windows Server 2008.
/// </summary>
public class DriverMiddleware : IMiddleware
{
    private const string FileExtension  = ".inf";
    
    public Task InvokeAsync(PipelineContext context)
    {
        return Task.Run(() =>
        {
            var outPutPath = context.Get<string>("DriverOutPut");
            if (string.IsNullOrWhiteSpace(outPutPath))
                return;

            var patchPath = context.Get<string>("PatchPath");
            if (string.IsNullOrWhiteSpace(patchPath))
                return;

            var fieldMappings = context.Get<Dictionary<string, string>>("FieldMappings");
            if (fieldMappings == null || fieldMappings.Count == 0)
                return;

            var disableUACScriptPath = context.Get<string>("DisableUACScriptPath");
            var restoreUACScriptPath = context.Get<string>("RestoreUACScriptPath");

            var builder = new DriverInformation.Builder()
                .SetDriverFileExtension(FileExtension)
                .SetOutPutDirectory(outPutPath)
                .SetDriverDirectory(patchPath)
                .SetFieldMappings(fieldMappings);
            
            if (!string.IsNullOrWhiteSpace(disableUACScriptPath))
                builder.SetDisableUACScriptPath(disableUACScriptPath);
            
            if (!string.IsNullOrWhiteSpace(restoreUACScriptPath))
                builder.SetRestoreUACScriptPath(restoreUACScriptPath);
            
            var information = builder.Build();

            var processor = new DriverProcessor();
            
            // Disable UAC before driver operations if script path is provided
            if (!string.IsNullOrWhiteSpace(information.DisableUACScriptPath))
                processor.AddCommand(new DisableUACCommand(information.DisableUACScriptPath));
            
            processor.AddCommand(new BackupDriverCommand(information));
            processor.AddCommand(new DeleteDriverCommand(information));
            processor.AddCommand(new InstallDriverCommand(information));
            
            // Restore UAC after driver operations if script path is provided
            if (!string.IsNullOrWhiteSpace(information.RestoreUACScriptPath))
                processor.AddCommand(new RestoreUACCommand(information.RestoreUACScriptPath));
            
            processor.ProcessCommands();
        });
    }
}