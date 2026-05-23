using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace GeneralUpdate.Core.Security;

public interface ISslValidationPolicy
{
    bool ValidateCertificate(
        X509Certificate2? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors);
}

public sealed class StrictSslValidationPolicy : ISslValidationPolicy
{
    public bool ValidateCertificate(
        X509Certificate2? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
        => sslPolicyErrors == SslPolicyErrors.None;
}
