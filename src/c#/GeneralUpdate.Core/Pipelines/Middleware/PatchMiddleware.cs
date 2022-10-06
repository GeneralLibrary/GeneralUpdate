using GeneralUpdate.Core.Domain.Enum;
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
                context.OnProgressEventAction(this, ProgressType.Patch, "Update patch file ...");
                await DifferentialCore.Instance.Drity(context.SourcePath, context.TargetPath);
                var node = stack.Pop();
                if (node != null) await node.Next.Invoke(context, stack);
            }
            catch (Exception ex)
            {
                var exception = new Exception($"{ ex.Message } !", ex.InnerException);
                context.OnExceptionEventAction(this, exception);
                throw exception;
            }
        }
    }
}