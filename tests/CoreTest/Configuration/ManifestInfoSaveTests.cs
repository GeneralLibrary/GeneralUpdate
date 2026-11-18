using System;
using System.IO;
using GeneralUpdate.Core.Configuration;
using Xunit;

namespace CoreTest.Configuration;

/// <summary>
/// Tests for <see cref="ManifestInfo.Save"/> atomicity and the
/// load/modify/save round trip that drives the update loop's version tracking.
/// </summary>
public class ManifestInfoSaveTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"ManifestSave_{Guid.NewGuid():N}");

    public ManifestInfoSaveTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* ignore */ }
    }

    private string ManifestPath => Path.Combine(_dir, ManifestInfo.FileName);

    [Fact]
    public void Save_CreatesReadableManifest()
    {
        var manifest = new ManifestInfo
        {
            MainAppName = "app.exe",
            UpdateAppName = "Update.exe",
            ClientVersion = "2.1.0",
            ProductId = "prod-1"
        };

        manifest.Save(ManifestPath);

        Assert.True(File.Exists(ManifestPath));
        var loaded = ManifestInfo.Load(ManifestPath);
        Assert.NotNull(loaded);
        Assert.Equal("2.1.0", loaded!.ClientVersion);
        Assert.Equal("app.exe", loaded.MainAppName);
        Assert.Equal("prod-1", loaded.ProductId);
    }

    [Fact]
    public void Save_LeavesNoTempResidue()
    {
        // The manifest drives the next update cycle's version decision; the writer
        // publishes via a sibling ".tmp-*" file that must be gone once the call
        // returns, or stale temp files accumulate in the install directory.
        var manifest = new ManifestInfo { ClientVersion = "1.0.0" };

        manifest.Save(ManifestPath);

        Assert.True(File.Exists(ManifestPath));
        var residue = Directory.GetFiles(_dir, ManifestInfo.FileName + ".tmp-*");
        Assert.Empty(residue);
    }

    [Fact]
    public void Save_OverwritesPreviousVersion()
    {
        // UpdateStrategy writes the new version back after every successful update;
        // the second save must fully replace the first file (the old writer could
        // leave a torn file if the process died mid-write).
        var manifest = new ManifestInfo { ClientVersion = "1.0.0" };
        manifest.Save(ManifestPath);

        manifest.ClientVersion = "3.0.0";
        manifest.Save(ManifestPath);

        var loaded = ManifestInfo.Load(ManifestPath);
        Assert.NotNull(loaded);
        Assert.Equal("3.0.0", loaded!.ClientVersion);
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp-*"));
    }
}
