using GeneralUpdate.Core.Security;

namespace CoreTest.Security;

public class AuthProviderTests
{
    [Fact]
    public async Task BearerTokenAuthProvider_AppliesAuthorizationHeader()
    {
        var provider = new BearerTokenAuthProvider("token-abc123");
        var request = new HttpRequestMessage();
        await provider.ApplyAuthAsync(request);
        Assert.NotNull(request.Headers.Authorization);
        Assert.Equal("Bearer", request.Headers.Authorization.Scheme);
        Assert.Equal("token-abc123", request.Headers.Authorization.Parameter);
    }

    [Fact]
    public void BearerTokenAuthProvider_NullToken_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new BearerTokenAuthProvider(null));
    }

    [Fact]
    public async Task ApiKeyAuthProvider_AppliesCustomHeader()
    {
        var provider = new ApiKeyAuthProvider("my-api-key-123");
        var request = new HttpRequestMessage();
        await provider.ApplyAuthAsync(request);
        Assert.True(request.Headers.Contains("X-Api-Key"));
        var values = request.Headers.GetValues("X-Api-Key").ToList();
        Assert.Single(values);
        Assert.Equal("my-api-key-123", values[0]);
    }

    [Fact]
    public void ApiKeyAuthProvider_NullApiKey_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ApiKeyAuthProvider(null));
    }

    [Fact]
    public async Task ApiKeyAuthProvider_CustomHeaderName()
    {
        var provider = new ApiKeyAuthProvider("key123", "X-Custom-Header");
        var request = new HttpRequestMessage();
        await provider.ApplyAuthAsync(request);
        Assert.True(request.Headers.Contains("X-Custom-Header"));
    }

    [Fact]
    public async Task NoOpAuthProvider_DoesNotModifyRequest()
    {
        var provider = new NoOpAuthProvider();
        var request = new HttpRequestMessage();
        var headerCountBefore = request.Headers.Count();
        await provider.ApplyAuthAsync(request);
        Assert.Equal(headerCountBefore, request.Headers.Count());
    }

    [Fact]
    public void Factory_HmacHasPriority_WhenSecretKeyPresent()
    {
        var provider = HttpAuthProviderFactory.Create("bearer", "token", "secret");
        Assert.IsType<HmacAuthProvider>(provider);
    }

    [Fact]
    public void Factory_TokenWithBearerScheme_ReturnsBearerTokenAuth()
    {
        var provider = HttpAuthProviderFactory.Create("bearer", "token", null);
        Assert.IsType<BearerTokenAuthProvider>(provider);
    }

    [Fact]
    public void Factory_TokenWithApiKeyScheme_ReturnsApiKeyAuth()
    {
        var provider = HttpAuthProviderFactory.Create("apikey", "token", null);
        Assert.IsType<ApiKeyAuthProvider>(provider);
    }

    [Fact]
    public void Factory_TokenWithUnknownScheme_ReturnsBearerTokenAuth()
    {
        var provider = HttpAuthProviderFactory.Create("unknown", "token", null);
        Assert.IsType<BearerTokenAuthProvider>(provider);
    }

    [Fact]
    public void Factory_NoTokenNoSecret_ReturnsNoOp()
    {
        var provider = HttpAuthProviderFactory.Create(null, null, null);
        Assert.IsType<NoOpAuthProvider>(provider);
    }

    [Fact]
    public void Factory_EmptyToken_ReturnsNoOp()
    {
        var provider = HttpAuthProviderFactory.Create(null, "", null);
        Assert.IsType<NoOpAuthProvider>(provider);
    }
}
