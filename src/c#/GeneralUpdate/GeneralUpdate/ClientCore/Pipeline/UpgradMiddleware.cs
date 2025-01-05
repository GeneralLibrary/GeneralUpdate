using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using GeneralUpdate.Common.Compress;
using GeneralUpdate.Common.Internal;
using GeneralUpdate.Common.Internal.Event;
using GeneralUpdate.Common.Internal.Pipeline;
using GeneralUpdate.Common.Shared.Object;

namespace GeneralUpdate.ClientCore.Pipeline;

public class UpgradMiddleware : IMiddleware
{
    public Task InvokeAsync(PipelineContext? context)
    {
        return Task.Run(() =>
        {

            var sourcePath = context.Get<string>("SourcePath");
            var patchPath = context.Get<string>("PatchPath");
            var upgradeName = context.Get<string>("UpgradeName");
            var newUpgradeName = Path.Combine(patchPath, upgradeName);
            if(File.Exists(newUpgradeName))
            {
                var oldUpgradeName = Path.Combine(sourcePath, upgradeName);
                if (File.Exists(oldUpgradeName))
                {
                    File.SetAttributes(oldUpgradeName, FileAttributes.Normal);
                    File.Delete(oldUpgradeName);
                }
                File.Move(newUpgradeName, oldUpgradeName);
            }

        });
    }
}