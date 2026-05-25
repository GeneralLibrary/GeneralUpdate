using GeneralUpdate.Core.Security;

namespace CoreTest.Network;

public class VersionServiceAuthTests
{
    [Fact]
    public void SetSslValidationPolicy_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            GeneralUpdate.Core.Network.VersionService.SetSslValidationPolicy(null));
    }

    [Fact]
    public void SetSslValidationPolicy_ValidPolicy_SetsGlobalPolicy()
    {
        var policy = new StrictSslValidationPolicy();
        var ex = Record.Exception(() =>
            GeneralUpdate.Core.Network.VersionService.SetSslValidationPolicy(policy));
        Assert.Null(ex);
    }

    [Fact]
    public void StrictSslValidationPolicy_NoErrors_ReturnsTrue()
    {
        var policy = new StrictSslValidationPolicy();
        var result = policy.ValidateCertificate(null, null, System.Net.Security.SslPolicyErrors.None);
        Assert.True(result);
    }

    [Fact]
    public void StrictSslValidationPolicy_AnyError_ReturnsFalse()
    {
        var policy = new StrictSslValidationPolicy();
        Assert.False(policy.ValidateCertificate(null, null,
            System.Net.Security.SslPolicyErrors.RemoteCertificateNotAvailable));
        Assert.False(policy.ValidateCertificate(null, null,
            System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch));
        Assert.False(policy.ValidateCertificate(null, null,
            System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors));
    }
}

public class HttpAuthProviderFactoryTests
{
    [Fact]
    public void Create_SecretKeyPresent_ReturnsHmacProvider()
    {
        var provider = HttpAuthProviderFactory.Create("bearer", "token", "secret-key");
        Assert.IsType<HmacAuthProvider>(provider);
    }

    [Fact]
    public void Create_TokenWithApiKeyScheme_ReturnsApiKeyProvider()
    {
        var provider = HttpAuthProviderFactory.Create("apikey", "my-api-key", null);
        Assert.IsType<ApiKeyAuthProvider>(provider);
    }

    [Fact]
    public void Create_TokenWithBearerScheme_ReturnsBearerTokenProvider()
    {
        var provider = HttpAuthProviderFactory.Create("bearer", "my-token", null);
        Assert.IsType<BearerTokenAuthProvider>(provider);
    }

    [Fact]
    public void Create_TokenWithUnknownScheme_DefaultsToBearer()
    {
        var provider = HttpAuthProviderFactory.Create(null, "my-token", null);
        Assert.IsType<BearerTokenAuthProvider>(provider);
    }

    [Fact]
    public void Create_NoTokenNoSecret_ReturnsNoOp()
    {
        var provider = HttpAuthProviderFactory.Create(null, null, null);
        Assert.IsType<NoOpAuthProvider>(provider);
    }

    [Fact]
    public void Create_EmptyToken_ReturnsNoOp()
    {
        var provider = HttpAuthProviderFactory.Create("bearer", "", null);
        Assert.IsType<NoOpAuthProvider>(provider);
    }

    [Fact]
    public void Create_WhitespaceToken_ReturnsBearerProvider()
    {
        // IsNullOrEmpty returns false for whitespace, so factory creates BearerTokenAuthProvider
        var provider = HttpAuthProviderFactory.Create("bearer", "   ", null);
        Assert.IsType<BearerTokenAuthProvider>(provider);
    }
}
