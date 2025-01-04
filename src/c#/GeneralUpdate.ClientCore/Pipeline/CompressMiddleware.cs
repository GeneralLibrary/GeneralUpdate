using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using GeneralUpdate.Common.Compress;
using GeneralUpdate.Common.Internal;
using GeneralUpdate.Common.Internal.Event;
using GeneralUpdate.Common.Internal.Pipeline;

namespace GeneralUpdate.ClientCore.Pipeline;

public class CompressMiddleware : IMiddleware
{
    public Task InvokeAsync(PipelineContext? context)
    {
        return Task.Run(() =>
        {
            var format = context.Get<string>("Format");
            var sourcePath = context.Get<string>("ZipFilePath");
            var patchPath = context.Get<string>("PatchPath");
            var encoding = context.Get<Encoding>("Encoding");
            CompressProvider.Decompress(format,sourcePath,patchPath, encoding);
        });
    }
}