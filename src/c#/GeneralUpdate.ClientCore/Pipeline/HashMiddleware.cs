using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using GeneralUpdate.Common.HashAlgorithms;
using GeneralUpdate.Common.Internal.Pipeline;
using GeneralUpdate.Common.Shared;

namespace GeneralUpdate.ClientCore.Pipeline;

public class HashMiddleware : IMiddleware
{
    public async Task InvokeAsync(PipelineContext context)
    {
        var path = context.Get<string>("ZipFilePath");
        var hash = context.Get<string>("Hash");
        GeneralTracer.Info($"ClientCore.HashMiddleware.InvokeAsync: verifying hash for file={path}, expectedHash={hash}");
        try
        {
            var isVerify = await VerifyFileHash(path, hash);
            if (!isVerify)
            {
                GeneralTracer.Error($"ClientCore.HashMiddleware.InvokeAsync: hash verification failed for file={path}.");
                throw new CryptographicException("Hash verification failed .");
            }
            GeneralTracer.Info("ClientCore.HashMiddleware.InvokeAsync: hash verification passed.");
        }
        catch (CryptographicException)
        {
            throw;
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("ClientCore.HashMiddleware.InvokeAsync: unexpected exception during hash verification.", ex);
            throw;
        }
    }

    private Task<bool> VerifyFileHash(string path, string hash)
    {
        return Task.Run(() =>
        {
            var hashAlgorithm = new Sha256HashAlgorithm();
            var hashSha256 = hashAlgorithm.ComputeHash(path);
            return string.Equals(hash, hashSha256, StringComparison.OrdinalIgnoreCase);
        });
    }
}