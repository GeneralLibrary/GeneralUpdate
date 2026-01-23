using System;

namespace MyApp.Extensions.Packaging
{
    /// <summary>
    /// Represents the signature, certificate, and hash information for a plugin package.
    /// </summary>
    public class PackageSignature
    {
        /// <summary>
        /// Gets or sets the digital signature of the package.
        /// </summary>
        public string Signature { get; set; }

        /// <summary>
        /// Gets or sets the algorithm used for the signature (e.g., "RSA", "ECDSA").
        /// </summary>
        public string SignatureAlgorithm { get; set; }

        /// <summary>
        /// Gets or sets the certificate used to sign the package.
        /// </summary>
        public string Certificate { get; set; }

        /// <summary>
        /// Gets or sets the certificate chain for validation.
        /// </summary>
        public string[] CertificateChain { get; set; }

        /// <summary>
        /// Gets or sets the thumbprint of the certificate.
        /// </summary>
        public string CertificateThumbprint { get; set; }

        /// <summary>
        /// Gets or sets the hash of the entire package.
        /// </summary>
        public string PackageHash { get; set; }

        /// <summary>
        /// Gets or sets the hash algorithm used for the package hash (e.g., "SHA256", "SHA512").
        /// </summary>
        public string HashAlgorithm { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the package was signed.
        /// </summary>
        public DateTime SignedTimestamp { get; set; }

        /// <summary>
        /// Gets or sets the URL of the timestamp authority.
        /// </summary>
        public string TimestampAuthorityUrl { get; set; }
    }
}
