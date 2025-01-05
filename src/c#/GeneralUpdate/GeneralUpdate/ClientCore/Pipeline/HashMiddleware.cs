using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading.Tasks;
using GeneralUpdate.Common.HashAlgorithms;
using GeneralUpdate.Common.Internal;
using GeneralUpdate.Common.Internal.Event;
using GeneralUpdate.Common.Internal.Pipeline;

namespace GeneralUpdate.ClientCore.Pipeline;

public class HashMiddleware : IMiddleware
{
    public async Task InvokeAsync(PipelineContext context)
    {
        var hash = context.Get<string>("Hash");
        if (!string.IsNullOrEmpty(hash))
        {
            var path = context.Get<string>("ZipFilePath");
            var isVerify = await VerifyFileHash(path, hash);
            if (!isVerify) throw new CryptographicException("Hash verification failed .");
        }
    }

    private Task<bool> VerifyFileHash(string path, string hash)
    {
        return Task.Run(() =>
        {
            try
            {
                var hashAlgorithm = new Sha256HashAlgorithm();
                var hashSha256 = hashAlgorithm.ComputeHash(path);
                return string.Equals(hash, hashSha256, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.Message);
                EventManager.Instance.Dispatch(this, new ExceptionEventArgs(exception, exception.Message));
            }
            return false;
        });
    }
}