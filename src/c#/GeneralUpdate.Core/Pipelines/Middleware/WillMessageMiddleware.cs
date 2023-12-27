using GeneralUpdate.Core.Pipelines.Context;
using GeneralUpdate.Core.WillMessage;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Pipelines.Middleware
{
    internal class WillMessageMiddleware : IMiddleware
    {
        public async Task InvokeAsync(BaseContext context, MiddlewareStack stack)
        {
            WillMessageManager.Instance.Backup(context.SourcePath, context.TargetPath, context.Version.ToString(), context.Version.Hash ,context.AppType);
            var node = stack.Pop();
            if (node != null) await node.Next.Invoke(context, stack);
        }
    }
}
