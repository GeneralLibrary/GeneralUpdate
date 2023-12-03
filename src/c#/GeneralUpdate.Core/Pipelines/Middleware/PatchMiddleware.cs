using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.Events;
using GeneralUpdate.Core.Events.MultiEventArgs;
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
            EventManager.Instance.Dispatch<Action<object, MultiDownloadProgressChangedEventArgs>>(this, new MultiDownloadProgressChangedEventArgs(context.Version, ProgressType.Patch, "Update patch file ..."));
            DifferentialCore.Instance.SetBlocklist(context.BlackFiles, context.BlackFileFormats);
            await DifferentialCore.Instance.Dirty(context.SourcePath, context.TargetPath);
            var node = stack.Pop();
            if (node != null) await node.Next.Invoke(context, stack);
        }
    }
}