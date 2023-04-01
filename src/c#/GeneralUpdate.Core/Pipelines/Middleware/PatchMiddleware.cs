using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.Events;
using GeneralUpdate.Core.Events.CommonArgs;
using GeneralUpdate.Core.Events.MutiEventArgs;
using GeneralUpdate.Core.Pipelines.Context;
using GeneralUpdate.Differential;
using System;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Pipelines.Middleware
{
    public class PatchMiddleware : IMiddleware
    {
        public async Task InvokeAsync(BaseContext context, MiddlewareStack stack)
        {
            try
            {
                EventManager.Instance.Dispatch<Action<object, MutiDownloadProgressChangedEventArgs>>(this, new MutiDownloadProgressChangedEventArgs(context.Version, ProgressType.Patch, "Update patch file ..."));
                DifferentialCore.Instance.SetBlocklist(context.BlackFiles, context.BlackFileFormats);
                await DifferentialCore.Instance.Drity(context.SourcePath, context.TargetPath);
                var node = stack.Pop();
                if (node != null) await node.Next.Invoke(context, stack);
            }
            catch (Exception ex)
            {
                var exception = new Exception($"{ex.Message} !", ex.InnerException);
                EventManager.Instance.Dispatch<Action<object, ExceptionEventArgs>>(this, new ExceptionEventArgs(exception));
            }
        }
    }
}