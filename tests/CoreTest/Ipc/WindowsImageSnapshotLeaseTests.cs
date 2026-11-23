#if WINDOWS_SNAPSHOT_LAUNCH_EXPERIMENT
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using GeneralUpdate.Core.Security;
using Microsoft.Win32.SafeHandles;

namespace CoreTest.Ipc;

public sealed class WindowsImageSnapshotLeaseTests
{
    [Fact]
    public void DisabledProfileIsRejected()
    {
        var image = new WindowsSnapshotFile("unused.exe", new byte[32]);
        if (OperatingSystem.IsWindows())
            Assert.Throws<InvalidOperationException>(() => WindowsImageSnapshotLease.Create(false, "unused", image, Array.Empty<WindowsSnapshotFile>()));
        else
            Assert.Throws<PlatformNotSupportedException>(() => WindowsImageSnapshotLease.Create(false, "unused", image, Array.Empty<WindowsSnapshotFile>()));
    }

    [Fact]
    public void VerifiedSnapshotHoldsImageAndDirectory()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new Fixture();
        using var lease = fixture.Create();
        Assert.Equal(File.ReadAllBytes(fixture.Image), File.ReadAllBytes(lease.ImagePath));
        Assert.Equal(File.ReadAllBytes(fixture.Dependency), File.ReadAllBytes(Path.Combine(lease.DirectoryPath, "native.dll")));
        var security = new DirectoryInfo(lease.DirectoryPath).GetAccessControl();
        Assert.True(security.AreAccessRulesProtected);
        Assert.Equal(WindowsIdentity.GetCurrent().User, security.GetOwner(typeof(SecurityIdentifier)));
        var write = Assert.Throws<IOException>(() => File.WriteAllText(lease.ImagePath, "changed"));
        Assert.Equal(32, write.HResult & 0xffff);
        var move = Assert.Throws<IOException>(() => Directory.Move(lease.DirectoryPath, lease.DirectoryPath + ".moved"));
        Assert.Equal(32, move.HResult & 0xffff);
    }

    [Fact]
    public void HashMismatchAndExistingWriteHandleAreRejected()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new Fixture();
        var bad = new WindowsSnapshotFile(fixture.Image, new byte[32]);
        Assert.Throws<InvalidDataException>(() => WindowsImageSnapshotLease.Create(true, fixture.Staging, bad, Array.Empty<WindowsSnapshotFile>()));
        var image = new WindowsSnapshotFile(fixture.Image, SHA256.HashData(File.ReadAllBytes(fixture.Image)));
        using var existing = new FileStream(fixture.Image, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        var failure = Assert.Throws<Win32Exception>(() =>
            WindowsImageSnapshotLease.Create(true, fixture.Staging, image, Array.Empty<WindowsSnapshotFile>()));
        Assert.Equal(32, failure.NativeErrorCode);
    }

    [Fact]
    public void UntrustedWriteRuleIsRejected()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new Fixture();
        var security = new FileInfo(fixture.Image).GetAccessControl();
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            FileSystemRights.WriteData, AccessControlType.Allow));
        new FileInfo(fixture.Image).SetAccessControl(security);
        Assert.Throws<InvalidDataException>(() => fixture.Create());
    }

    [Fact]
    public void ExistingDeleteHandleAndAncestorJunctionAreRejected()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new Fixture();
        var image = new WindowsSnapshotFile(fixture.Image, SHA256.HashData(File.ReadAllBytes(fixture.Image)));
        using (var handle = CreateFile(fixture.Image, 0x00010000, 7, IntPtr.Zero, 3, 0x80, IntPtr.Zero))
        {
            if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
            var failure = Assert.Throws<Win32Exception>(() =>
                WindowsImageSnapshotLease.Create(true, fixture.Staging, image, Array.Empty<WindowsSnapshotFile>()));
            Assert.Equal(32, failure.NativeErrorCode);
        }
        var junction = Path.Combine(fixture.Root, "junction");
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[] { "/c", "mklink", "/J", junction, fixture.Root })
            start.ArgumentList.Add(argument);
        using var command = Process.Start(start) ?? throw new InvalidOperationException("Could not start mklink.");
        var output = command.StandardOutput.ReadToEnd();
        var error = command.StandardError.ReadToEnd();
        command.WaitForExit();
        Assert.True(command.ExitCode == 0, output + error);
        try
        {
            var throughJunction = new WindowsSnapshotFile(Path.Combine(junction, "image.exe"),
                SHA256.HashData(File.ReadAllBytes(fixture.Image)));
            Assert.Throws<InvalidDataException>(() =>
                WindowsImageSnapshotLease.Create(true, fixture.Staging, throughJunction, Array.Empty<WindowsSnapshotFile>()));
        }
        finally { Directory.Delete(junction); }
    }

    [Fact]
    public async Task LaunchUsesTheSnapshotAndCancellationEndsTheJob()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new Fixture();
        var native = Environment.GetEnvironmentVariable("GU_WINDOWS_NATIVE_LOCK_PEER");
        var dependencies = Array.Empty<WindowsSnapshotFile>();
        string[] arguments;
        var marker = Path.Combine(fixture.Root, "child marker.txt");
        if (native != null)
        {
            File.Copy(native, fixture.Image, overwrite: true);
            var sqlite = Path.Combine(fixture.Root, "e_sqlite3.dll");
            File.Copy(Path.Combine(Path.GetDirectoryName(native)!, "e_sqlite3.dll"), sqlite);
            dependencies = new[] { new WindowsSnapshotFile(sqlite, SHA256.HashData(File.ReadAllBytes(sqlite))) };
            arguments = new[] { "--snapshot-probe", marker, "wait" };
        }
        else
        {
            File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), fixture.Image, overwrite: true);
            arguments = new[] { "/c", "ping", "-n", "120", "127.0.0.1" };
        }
        var image = new WindowsSnapshotFile(fixture.Image, SHA256.HashData(File.ReadAllBytes(fixture.Image)));
        var lease = WindowsNativeLaunchLease.Start(true, fixture.Staging, image, dependencies, arguments);
        await using (lease)
        {
            if (native != null)
            {
                for (var i = 0; i < 200 && !File.Exists(marker); i++) await Task.Delay(50);
                Assert.True(File.Exists(marker), "Native AOT peer did not load SQLite and create the marker.");
                Assert.Equal(Path.Combine(lease.SnapshotDirectory, "e_sqlite3.dll"), File.ReadAllText(marker), ignoreCase: true);
            }
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => lease.WaitForExitAsync(timeout.Token));
        }
    }

    [Fact]
    public async Task InvalidImageHashNeverStartsAndNormalExitIsReported()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new Fixture();
        File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), fixture.Image, overwrite: true);
        var marker = Path.Combine(fixture.Root, "unexpected.txt");
        var bad = new WindowsSnapshotFile(fixture.Image, new byte[32]);
        Assert.Throws<InvalidDataException>(() => WindowsNativeLaunchLease.Start(true, fixture.Staging, bad,
            Array.Empty<WindowsSnapshotFile>(), new[] { "/c", "echo", "bad", ">", marker }));
        Assert.False(File.Exists(marker));
        var image = new WindowsSnapshotFile(fixture.Image, SHA256.HashData(File.ReadAllBytes(fixture.Image)));
        await using var lease = WindowsNativeLaunchLease.Start(true, fixture.Staging, image,
            Array.Empty<WindowsSnapshotFile>(), new[] { "/c", "exit 1" });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        Assert.Equal(1u, await lease.WaitForExitAsync(timeout.Token));
    }

    [SupportedOSPlatform("windows")]
    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "gu-snapshot-test-" + Guid.NewGuid().ToString("N"));
        public string Root => _root;
        public string Staging { get; }
        public string Image { get; }
        public string Dependency { get; }

        public Fixture()
        {
            var user = WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException();
            var security = new DirectorySecurity();
            security.SetOwner(user);
            security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None, AccessControlType.Allow));
            new DirectoryInfo(_root).Create(security);
            Staging = Path.Combine(_root, "staging");
            new DirectoryInfo(Staging).Create(security);
            Image = Path.Combine(_root, "image.exe");
            Dependency = Path.Combine(_root, "native.dll");
            File.WriteAllText(Image, "image bytes");
            File.WriteAllText(Dependency, "dependency bytes");
        }
        #endif

        public WindowsImageSnapshotLease Create() => WindowsImageSnapshotLease.Create(true, Staging,
            new WindowsSnapshotFile(Image, SHA256.HashData(File.ReadAllBytes(Image))),
            new[] { new WindowsSnapshotFile(Dependency, SHA256.HashData(File.ReadAllBytes(Dependency))) });

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security,
        uint creation, uint flags, IntPtr template);
}
