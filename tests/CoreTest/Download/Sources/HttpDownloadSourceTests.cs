using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download.Abstractions;
using GeneralUpdate.Core.Download.Sources;

namespace CoreTest.Download.Sources;

/// <summary>
/// Unit tests for <see cref="HttpDownloadSource"/> following AAAT pattern.
/// Tests constructor validation, interface implementation, and basic behaviour.
/// </summary>
public class HttpDownloadSourceTests
{
    private const string ValidUrl = "http://localhost:5000/api/versions";
    private const string ClientVersion = "1.0.0";
    private const string AppSecretKey = "test-secret-key";

    #region Constructor — valid inputs

    [Fact]
    public void Ctor_WithAllRequiredParams_CreatesInstance()
    {
        var source = new HttpDownloadSource(
            ValidUrl, ClientVersion, null, AppSecretKey,
            PlatformType.Windows, null, null, null);

        Assert.NotNull(source);
    }

    [Fact]
    public void Ctor_WithAllOptionalParams_CreatesInstance()
    {
        var source = new HttpDownloadSource(
            ValidUrl, ClientVersion, "2.0.0", AppSecretKey,
            PlatformType.Linux, "product-1", "Bearer", "token-abc");

        Assert.NotNull(source);
    }

    [Fact]
    public void Ctor_WithUpgradeClientVersion_CreatesInstance()
    {
        var source = new HttpDownloadSource(
            ValidUrl, ClientVersion, "1.5.0", AppSecretKey,
            PlatformType.Windows, null, null, null);

        Assert.NotNull(source);
    }

    [Fact]
    public void Ctor_WithMacOSPlatform_CreatesInstance()
    {
        var source = new HttpDownloadSource(
            ValidUrl, ClientVersion, null, AppSecretKey,
            PlatformType.MacOS, null, null, null);

        Assert.NotNull(source);
    }

    #endregion

    #region Constructor — edge cases

    [Fact]
    public void Ctor_WithEmptyClientVersion_CreatesInstance()
    {
        var source = new HttpDownloadSource(
            ValidUrl, string.Empty, null, AppSecretKey,
            PlatformType.Windows, null, null, null);

        Assert.NotNull(source);
    }

    [Fact]
    public void Ctor_WithEmptyProductId_CreatesInstance()
    {
        var source = new HttpDownloadSource(
            ValidUrl, ClientVersion, null, AppSecretKey,
            PlatformType.Windows, string.Empty, null, null);

        Assert.NotNull(source);
    }

    #endregion

    #region IDownloadSource contract

    [Fact]
    public void Implements_IDownloadSource()
    {
        var source = new HttpDownloadSource(
            ValidUrl, ClientVersion, null, AppSecretKey,
            PlatformType.Windows, null, null, null);

        Assert.IsAssignableFrom<IDownloadSource>(source);
    }

    #endregion
}
