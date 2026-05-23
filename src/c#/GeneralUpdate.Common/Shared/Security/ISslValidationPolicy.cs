using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace GeneralUpdate.Common.Shared.Security;

/// <summary>
/// SSL/TLS certificate validation policy.
/// Implement this interface to customize certificate validation behavior
/// (e.g., certificate pinning, custom CA trust, or bypass for testing environments).
/// </summary>
public interface ISslValidationPolicy
{
    /// <summary>
    /// Validate the server certificate presented during TLS handshake.
    /// </summary>
    /// <param name="certificate">The server certificate, or null if not presented.</param>
    /// <param name="chain">The certificate chain built by the system.</param>
    /// <param name="sslPolicyErrors">Errors detected by the system's default validation.</param>
    /// <returns>true to accept the certificate; false to reject and abort the connection.</returns>
    bool ValidateCertificate(
        X509Certificate2? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors);
}

/// <summary>
/// Default strict SSL validation policy.
/// Accepts only certificates that pass all standard validation checks
/// (trusted root CA, correct hostname, not expired, not revoked).
/// This is the safe default and should be used in production.
/// </summary>
public sealed class StrictSslValidationPolicy : ISslValidationPolicy
{
    /// <inheritdoc />
    public bool ValidateCertificate(
        X509Certificate2? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
        => sslPolicyErrors == SslPolicyErrors.None;
}
