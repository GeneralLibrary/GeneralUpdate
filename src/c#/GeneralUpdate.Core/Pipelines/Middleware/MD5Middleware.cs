using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.Events;
using GeneralUpdate.Core.Events.CommonArgs;
using GeneralUpdate.Core.Events.MultiEventArgs;
using GeneralUpdate.Core.Pipelines.Context;
using GeneralUpdate.Core.Utils;
using System;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Pipelines.Middleware
{
    public class MD5Middleware : IMiddleware
    {
        public async Task InvokeAsync(BaseContext context, MiddlewareStack stack)
        {
            Exception exception = null;
            try
            {
                EventManager.Instance.Dispatch<Action<object, MultiDownloadProgressChangedEventArgs>>(this, new MultiDownloadProgressChangedEventArgs(context.Version, ProgressType.MD5, "Verify file MD5 code ..."));
                var version = context.Version;
                bool isVerify = VerifyFileMd5(context.ZipfilePath, version.MD5);
                if (!isVerify) exception = new Exception($"The update package MD5 code is inconsistent ! version-{version.Version}  MD5-{version.MD5} .");
                var node = stack.Pop();
                if (node != null) await node.Next.Invoke(context, stack);
            }
            catch (Exception ex)
            {
                exception = exception ?? ex;
                EventManager.Instance.Dispatch<Action<object, ExceptionEventArgs>>(this, new ExceptionEventArgs(exception));
                throw exception;
            }
        }

        private bool VerifyFileMd5(string fileName, string md5)
        {
            var packetMD5 = FileUtil.GetFileMD5(fileName);
            if (md5.ToUpper().Equals(packetMD5.ToUpper())) return true;
            return false;
        }
    }
}