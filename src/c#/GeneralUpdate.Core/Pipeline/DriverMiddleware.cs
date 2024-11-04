using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using GeneralUpdate.Common;
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
            var patchPath = context.Get<string>("PatchPath");
            if(string.IsNullOrWhiteSpace(patchPath))
                return;
            
            var outPutPath = context.Get<string>("DriverOutPut");
            if(string.IsNullOrWhiteSpace(outPutPath))
                return;

            var information = new DriverInformation.Builder()
                .SetDriverFileExtension(FileExtension)
                .SetOutPutDirectory(outPutPath)
                .SetDrivers(patchPath, FileExtension)
                .Build();

            var processor = new DriverProcessor();
            processor.AddCommand(new BackupDriverCommand(information));
            processor.AddCommand(new InstallDriverCommand(information));
            processor.ProcessCommands();
        });
    }
}