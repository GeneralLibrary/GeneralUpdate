using System.Collections.Generic;
using System.Threading.Tasks;
using GeneralUpdate.Common.FileBasic;
using GeneralUpdate.Common.Internal.Pipeline;
using GeneralUpdate.Differential;

namespace GeneralUpdate.ClientCore.Pipeline;

public class PatchMiddleware : IMiddleware
{
    public async Task InvokeAsync(PipelineContext context)
    {
        var sourcePath = context.Get<string>("SourcePath");
        var targetPath = context.Get<string>("PatchPath");
        var blackFiles = context.Get<List<string>>("BlackFiles");
        var blackFileFormats = context.Get<List<string>>("BlackFileFormats");

        BlackListManager.Instance.AddBlackFiles(blackFiles);
        BlackListManager.Instance.AddBlackFileFormats(blackFileFormats);
        await DifferentialCore.Instance.Dirty(sourcePath, targetPath);
    }
}