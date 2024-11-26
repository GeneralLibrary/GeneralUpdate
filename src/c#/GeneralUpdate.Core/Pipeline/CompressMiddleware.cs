using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using GeneralUpdate.Common.Compress;
using GeneralUpdate.Common.Internal;
using GeneralUpdate.Common.Internal.Event;
using GeneralUpdate.Common.Internal.Pipeline;

namespace GeneralUpdate.Core.Pipeline;

public class CompressMiddleware : IMiddleware
{
    public Task InvokeAsync(PipelineContext context)
    {
        return Task.Run(() =>
        {
            try
            {
                var format = context.Get<string>("Format");
                var name = context.Get<string>("Name");
                var sourcePath = context.Get<string>("ZipFilePath");
                var patchPath = context.Get<string>("PatchPath");
                var encoding = context.Get<Encoding>("Encoding");
                CompressProvider.Decompress(format, sourcePath, patchPath, encoding);
            }
            catch (Exception e)
            {
                EventManager.Instance.Dispatch(this, new ExceptionEventArgs(e, e.Message));
            }
        });
    }
}