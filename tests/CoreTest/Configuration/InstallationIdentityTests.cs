using GeneralUpdate.Core.Configuration;

namespace CoreTest.Configuration;

public sealed class InstallationIdentityTests
{
    [Fact]
    public void Identity_IsStableConcurrentAndScopedToInstallation()
    {
        var root = Path.Combine(Path.GetTempPath(), "installation-id-" + Guid.NewGuid().ToString("N"));
        try
        {
            var config = new UpdateContext { InstallPath = Path.Combine(root, "app"), DiagnosticsDirectory = Path.Combine(root, "state") };
            var ids = Enumerable.Range(0, 20).AsParallel().Select(_ => InstallationIdentity.GetOrCreate(config)).ToArray();
            Assert.Single(ids.Distinct());
            Assert.True(Guid.TryParseExact(ids[0], "D", out _));
            Assert.Equal(ids[0], InstallationIdentity.GetOrCreate(config));
            var other = new UpdateContext { InstallPath = Path.Combine(root, "other"), DiagnosticsDirectory = config.DiagnosticsDirectory };
            Assert.NotEqual(ids[0], InstallationIdentity.GetOrCreate(other));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void CorruptIdentity_FailsInsteadOfChangingDevice()
    {
        var root = Path.Combine(Path.GetTempPath(), "installation-id-" + Guid.NewGuid().ToString("N"));
        try
        {
            var config = new UpdateContext { InstallPath = Path.Combine(root, "app"), DiagnosticsDirectory = Path.Combine(root, "state") };
            InstallationIdentity.GetOrCreate(config);
            File.WriteAllText(Assert.Single(Directory.GetFiles(Path.Combine(config.DiagnosticsDirectory, "installations"), "*.id")), "corrupt");
            Assert.Throws<InvalidDataException>(() => InstallationIdentity.GetOrCreate(config));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
