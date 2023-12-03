using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.Events;
using GeneralUpdate.Core.Events.MultiEventArgs;
using GeneralUpdate.Core.Pipelines.Context;
using GeneralUpdate.Differential.Config;
using System;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Pipelines.Middleware
{
    [Obsolete("This feature is temporarily deprecated in the current version pending refactoring.")]
    public class ConfigMiddleware : IMiddleware
    {
        public async Task InvokeAsync(BaseContext context, MiddlewareStack stack)
        {
            EventManager.Instance.Dispatch<Action<object, MultiDownloadProgressChangedEventArgs>>(this, new MultiDownloadProgressChangedEventArgs(context.Version, ProgressType.Hash, "Update configuration file ..."));
            await ConfigFactory.Instance.Deploy();
            var node = stack.Pop();
            if (node != null) await node.Next.Invoke(context, stack);
        }
    }
}