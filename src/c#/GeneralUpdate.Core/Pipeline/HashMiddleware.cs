using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using GeneralUpdate.Core.HashAlgorithms;
using GeneralUpdate.Core.Pipeline;
using GeneralUpdate.Core;

namespace GeneralUpdate.Core.Pipeline;

/// <summary>
/// Hash verification middleware for validating the SHA256 integrity of a downloaded archive.
/// </summary>
/// <remarks>
/// <para>
/// This middleware reads the following keys from <see cref="PipelineContext"/>:
/// <list type="bullet">
///   <item><description><c>"ZipFilePath"</c> — The path to the downloaded archive file.</description></item>
///   <item><description><c>"Hash"</c> — The expected SHA256 hash value (hexadecimal string).</description></item>
/// </list>
/// </para>
/// <para>
/// Workflow:
/// <list type="number">
///   <item><description>Retrieves the archive path and expected hash from the context.</description></item>
///   <item><description>Computes the actual SHA256 hash of the file using <see cref="Sha256HashAlgorithm"/>.</description></item>
///   <item><description>Performs a case-insensitive comparison between the actual and expected hash values.</description></item>
///   <item><description>If they match, logs success and continues; if they do not match, throws a <see cref="CryptographicException"/> to terminate the pipeline.</description></item>
/// </list>
/// </para>
/// <para>
/// This middleware should be registered before <see cref="CompressMiddleware"/> to ensure the integrity
/// of the archive is verified before decompression.
/// </para>
/// </remarks>
public class HashMiddleware : IMiddleware
{
    /// <summary>
    /// Asynchronously executes the hash verification logic.
    /// </summary>
    /// <param name="context">The pipeline context containing the archive path and expected hash value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="CryptographicException">Thrown when the actual SHA256 hash of the file does not match the expected hash.</exception>
    /// <exception cref="Exception">Other exceptions that may occur during file reading or hash computation.</exception>
    /// <remarks>
    /// <para>
    /// This method represents the first security-check stage of the pipeline. It ensures that the downloaded
    /// archive has not been tampered with or corrupted during transit.
    /// </para>
    /// <para>
    /// Hash computation is performed on a background thread (via <see cref="Task.Run"/>) to avoid blocking
    /// the calling thread. On verification failure, the <see cref="CryptographicException"/> propagates upward
    /// to halt the entire pipeline execution.
    /// </para>
    /// </remarks>
    public async Task InvokeAsync(PipelineContext context)
    {
        var path = context.Get<string>("ZipFilePath");
        var hash = context.Get<string>("Hash");
        GeneralTracer.Info($"HashMiddleware.InvokeAsync: verifying hash for file={path}, expectedHash={hash}");
        try
        {
            var isVerify = await VerifyFileHash(path, hash);
            if (!isVerify)
            {
                GeneralTracer.Error($"HashMiddleware.InvokeAsync: hash verification failed for file={path}.");
                throw new CryptographicException("Hash verification failed !");
            }
            GeneralTracer.Info("HashMiddleware.InvokeAsync: hash verification passed.");
        }
        catch (CryptographicException)
        {
            throw;
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("HashMiddleware.InvokeAsync: unexpected exception during hash verification.", ex);
            throw;
        }
    }

    /// <summary>
    /// Computes the SHA256 hash of a file and compares it against the expected value.
    /// </summary>
    /// <param name="path">The full path to the file to verify.</param>
    /// <param name="hash">The expected SHA256 hash value (hexadecimal string).</param>
    /// <returns>
    /// <c>true</c> if the actual SHA256 hash of the file matches <paramref name="hash"/>
    /// (case-insensitive comparison); otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// Hash computation is performed on a background thread-pool thread via <see cref="Task.Run"/>
    /// to avoid blocking. Internally uses <see cref="Sha256HashAlgorithm"/> to compute the file hash,
    /// which implements the standard SHA256 hash algorithm. The comparison uses
    /// <see cref="StringComparison.OrdinalIgnoreCase"/> for case-insensitive hexadecimal string comparison.
    /// </remarks>
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
