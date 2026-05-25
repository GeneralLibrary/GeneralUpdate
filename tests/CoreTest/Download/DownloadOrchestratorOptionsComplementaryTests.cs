using GeneralUpdate.Core.Download.Models;

namespace CoreTest.Download;

// NOTE: DownloadOrchestratorOptionsTests already exists in this namespace.
// See existing OrchestratorOptionsBehaviourTests.cs and DownloadOrchestratorOptionsTests.cs
// This file adds complementary tests.

public class DownloadOrchestratorOptionsComplementaryTests
{
    [Fact]
    public void From_DownloadTimeOutNegative_DefaultsTo30Seconds()
    {
        var config = new GeneralUpdate.Core.Configuration.GlobalConfigInfo
        {
            DownloadTimeOut = -5
        };
        var opts = DownloadOrchestratorOptions.From(config);
        Assert.Equal(TimeSpan.FromSeconds(30), opts.DownloadTimeout);
    }

    [Fact]
    public void From_RetryCountNegative_ClampedToZero()
    {
        var config = new GeneralUpdate.Core.Configuration.GlobalConfigInfo
        {
            RetryCount = -5
        };
        var opts = DownloadOrchestratorOptions.From(config);
        Assert.Equal(0, opts.RetryCount);
    }
}
