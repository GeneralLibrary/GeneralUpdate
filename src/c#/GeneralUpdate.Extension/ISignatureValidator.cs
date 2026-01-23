using System.Threading.Tasks;

namespace MyApp.Extensions
{
    /// <summary>
    /// Provides methods for validating package signatures and integrity.
    /// </summary>
    public interface ISignatureValidator
    {
        /// <summary>
        /// Validates the signature of a package.
        /// </summary>
        /// <param name="packagePath">The path to the package.</param>
        /// <returns>A task that represents the asynchronous operation, containing the validation result.</returns>
        Task<SignatureValidationResult> ValidateSignatureAsync(string packagePath);

        /// <summary>
        /// Verifies the integrity of a package using its hash.
        /// </summary>
        /// <param name="packagePath">The path to the package.</param>
        /// <param name="expectedHash">The expected hash value.</param>
        /// <param name="hashAlgorithm">The hash algorithm to use (e.g., "SHA256").</param>
        /// <returns>A task that represents the asynchronous operation, indicating whether the integrity is valid.</returns>
        Task<bool> VerifyIntegrityAsync(string packagePath, string expectedHash, string hashAlgorithm);

        /// <summary>
        /// Validates the certificate chain for a package signature.
        /// </summary>
        /// <param name="packagePath">The path to the package.</param>
        /// <returns>A task that represents the asynchronous operation, indicating whether the certificate chain is valid.</returns>
        Task<bool> ValidateCertificateChainAsync(string packagePath);

        /// <summary>
        /// Checks whether a package is signed by a trusted authority.
        /// </summary>
        /// <param name="packagePath">The path to the package.</param>
        /// <returns>A task that represents the asynchronous operation, indicating whether the package is trusted.</returns>
        Task<bool> IsTrustedAsync(string packagePath);
    }

    /// <summary>
    /// Represents the result of a signature validation.
    /// </summary>
    public class SignatureValidationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the signature is valid.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the signature is trusted.
        /// </summary>
        public bool IsTrusted { get; set; }

        /// <summary>
        /// Gets or sets the signer's identity.
        /// </summary>
        public string SignerIdentity { get; set; }

        /// <summary>
        /// Gets or sets the certificate thumbprint.
        /// </summary>
        public string CertificateThumbprint { get; set; }

        /// <summary>
        /// Gets or sets any error messages.
        /// </summary>
        public string ErrorMessage { get; set; }
    }
}
