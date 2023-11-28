using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.Events;
using GeneralUpdate.Core.Events.CommonArgs;
using GeneralUpdate.Core.Events.MultiEventArgs;
using GeneralUpdate.Core.HashAlgorithms;
using GeneralUpdate.Core.Pipelines.Context;
using System;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Pipelines.Middleware
{
    public class HashMiddleware : IMiddleware
    {
        public async Task InvokeAsync(BaseContext context, MiddlewareStack stack)
        {
            Exception exception = null;
            try
            {
                EventManager.Instance.Dispatch<Action<object, MultiDownloadProgressChangedEventArgs>>(this, new MultiDownloadProgressChangedEventArgs(context.Version, ProgressType.Hash, "Verify file MD5 code ..."));
                var version = context.Version;
                bool isVerify = VerifyFileHash(context.ZipfilePath, version.Hash);
                if (!isVerify) exception = new Exception($"The update package hash code is inconsistent ! version-{version.Version}  hash-{version.Hash} .");
                var node = stack.Pop();
                if (node != null) await node.Next.Invoke(context, stack);
            }
            catch (Exception ex)
            {
                EventManager.Instance.Dispatch<Action<object, ExceptionEventArgs>>(this, new ExceptionEventArgs(exception ?? ex));
            }
        }

        private bool VerifyFileHash(string fileName, string hash)
        {
            var hashAlgorithm = new Sha256HashAlgorithm();
            var hashSha256 = hashAlgorithm.ComputeHash(fileName);
            return string.Equals(hash, hashSha256, StringComparison.OrdinalIgnoreCase);
        }
    }
}