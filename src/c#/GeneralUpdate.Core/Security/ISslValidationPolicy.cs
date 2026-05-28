using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace GeneralUpdate.Core.Security;

/// <summary>
/// Defines a strategy for validating SSL/TLS server certificates during HTTPS communication.
/// </summary>
/// <remarks>
/// <para>
/// Implementations of this interface encapsulate the logic for determining whether a
/// server certificate is trusted. The validation result controls whether the HTTPS
/// connection proceeds or is rejected.
/// </para>
/// <para>
/// The default implementation is <see cref="StrictSslValidationPolicy"/>, which rejects
/// any certificate with SSL policy errors. Custom implementations can be registered globally
/// via <see cref="Network.VersionService.SetSslValidationPolicy(ISslValidationPolicy)"/>
/// to relax or extend validation rules (e.g., for self-signed certificates in development
/// environments).
/// </para>
/// </remarks>
public interface ISslValidationPolicy
{
    /// <summary>
    /// Validates the server's SSL/TLS certificate.
    /// </summary>
    /// <param name="certificate">The server's X509 certificate, or <c>null</c> if not provided.</param>
    /// <param name="chain">The certificate chain, or <c>null</c> if not available.</param>
    /// <param name="sslPolicyErrors">The SSL policy errors detected during the validation handshake.</param>
    /// <returns><c>true</c> if the certificate is considered valid; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// Implementations should examine the <paramref name="sslPolicyErrors"/> value along with
    /// the certificate and chain to make a trust decision. Returning <c>false</c> will cause
    /// the HTTPS request to be aborted with an authentication error.
    /// </remarks>
    bool ValidateCertificate(
        X509Certificate2? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors);
}

/// <summary>
/// A strict SSL validation policy that only accepts certificates with no SSL policy errors.
/// </summary>
/// <remarks>
/// <para>
/// This is the default validation policy used by <see cref="Network.VersionService"/>.
/// It rejects any certificate that has detected policy errors, such as:
/// <list type="bullet">
///   <item><description>Name mismatch between the certificate and the hostname.</description></item>
///   <item><description>Certificate signed by an untrusted root authority.</description></item>
///   <item><description>Expired or not-yet-valid certificate.</description></item>
/// </list>
/// </para>
/// <para>
/// For development or testing scenarios with self-signed certificates, replace this policy
/// with a custom implementation by calling
/// <see cref="Network.VersionService.SetSslValidationPolicy(ISslValidationPolicy)"/>.
/// </para>
/// </remarks>
public sealed class StrictSslValidationPolicy : ISslValidationPolicy
{
    /// <summary>
    /// Validates the certificate by checking whether SSL policy errors are present.
    /// </summary>
    /// <param name="certificate">The server's X509 certificate. Ignored by this implementation.</param>
    /// <param name="chain">The certificate chain. Ignored by this implementation.</param>
    /// <param name="sslPolicyErrors">The SSL policy errors detected during validation.</param>
    /// <returns><c>true</c> only when <paramref name="sslPolicyErrors"/> is <see cref="SslPolicyErrors.None"/>.</returns>
    public bool ValidateCertificate(
        X509Certificate2? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
        => sslPolicyErrors == SslPolicyErrors.None;
}
