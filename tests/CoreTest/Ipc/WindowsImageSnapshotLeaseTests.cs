using System.ComponentModel;
using System.Diagnostics;
using System.Reflection.PortableExecutable;
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
        {
            Assert.Throws<InvalidOperationException>(() => WindowsImageSnapshotLease.Create(false, "unused", image, Array.Empty<WindowsSnapshotFile>()));
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            {
                Assert.Throws<InvalidOperationException>(() => WindowsNativeLaunchLease.Start(
                    WindowsNativeLaunchProfile.Disabled, "unused", image, Array.Empty<WindowsSnapshotFile>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>()));
                Assert.Throws<InvalidOperationException>(() => WindowsNativeLaunchLease.Start(
                    (WindowsNativeLaunchProfile)42, "unused", image, Array.Empty<WindowsSnapshotFile>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>()));
            }
        }
        else
        {
            Assert.Throws<PlatformNotSupportedException>(() => WindowsImageSnapshotLease.Create(false, "unused", image, Array.Empty<WindowsSnapshotFile>()));
            Assert.Throws<PlatformNotSupportedException>(() => WindowsNativeLaunchLease.Start(
                WindowsNativeLaunchProfile.TrustedWindowsSystemAndCooperativeNativeAot,
                "unused", image, Array.Empty<WindowsSnapshotFile>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>()));
        }
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
    public void DisposalDeletesTheHeldSnapshot()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new Fixture();
        var lease = fixture.Create();
        var path = lease.DirectoryPath;
        lease.Dispose();
        Assert.False(Directory.Exists(path));
        lease.Dispose();
    }

    [Fact]
    public void DeploymentCanReplaceSourceWhileSnapshotRemainsFixed()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new Fixture();
        using var lease = fixture.Create();
        var replacement = fixture.Image + ".new";
        File.WriteAllText(replacement, "new deployment");
        File.Move(replacement, fixture.Image, overwrite: true);
        Assert.Equal("new deployment", File.ReadAllText(fixture.Image));
        Assert.Equal("image bytes", File.ReadAllText(lease.ImagePath));
    }

    [Fact]
    public void UnexpectedChildIsNotRecursivelyDeleted()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new Fixture();
        var lease = fixture.Create();
        var path = Path.Combine(lease.DirectoryPath, "unexpected.txt");
        File.WriteAllText(path, "preserve");
        var failure = Assert.Throws<Win32Exception>(() => lease.Dispose());
        Assert.Equal(145, failure.NativeErrorCode);
        Assert.Equal("preserve", File.ReadAllText(path));
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
        var native = Environment.GetEnvironmentVariable("GU_WINDOWS_SNAPSHOT_PEER");
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
        var systems = TrustedModules(new[] { fixture.Image }.Concat(dependencies.Select(item => item.SourcePath)));
        var lease = WindowsNativeLaunchLease.Start(WindowsNativeLaunchProfile.TrustedWindowsSystemAndCooperativeNativeAot,
            fixture.Staging, image, dependencies, dependencies.Select(item => Path.GetFileName(item.SourcePath)).ToArray(), systems, arguments);
        await using (lease)
        {
            using (var process = lease.DuplicateProcessHandle())
            {
                Assert.False(TerminateProcess(process, 123));
                Assert.Equal(5, Marshal.GetLastWin32Error());
            }
            if (native != null)
            {
                for (var i = 0; i < 200 && !File.Exists(marker + ".ready") && !File.Exists(marker + ".error"); i++) await Task.Delay(50);
                Assert.True(File.Exists(marker + ".ready"), File.Exists(marker + ".error") ? File.ReadAllText(marker + ".error") : "Native AOT peer did not load SQLite and create the marker.");
                Assert.Equal(Path.Combine(lease.SnapshotDirectory, "e_sqlite3.dll"), File.ReadAllLines(marker)[0], ignoreCase: true);
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
        Assert.Throws<InvalidDataException>(() => WindowsNativeLaunchLease.Start(WindowsNativeLaunchProfile.TrustedWindowsSystemAndCooperativeNativeAot, fixture.Staging, bad,
            Array.Empty<WindowsSnapshotFile>(), Array.Empty<string>(), Array.Empty<string>(), new[] { "/c", "echo", "bad", ">", marker }));
        var good = new WindowsSnapshotFile(fixture.Image, SHA256.HashData(File.ReadAllBytes(fixture.Image)));
        var badDependency = new WindowsSnapshotFile(fixture.Dependency, new byte[32]);
        Assert.Throws<InvalidDataException>(() => WindowsNativeLaunchLease.Start(WindowsNativeLaunchProfile.TrustedWindowsSystemAndCooperativeNativeAot,
            fixture.Staging, good, new[] { badDependency }, new[] { "native.dll" }, Array.Empty<string>(), new[] { "/c", "echo", "bad", ">", marker }));
        Assert.Throws<InvalidDataException>(() => WindowsNativeLaunchLease.Start(WindowsNativeLaunchProfile.TrustedWindowsSystemAndCooperativeNativeAot,
            fixture.Staging, good, Array.Empty<WindowsSnapshotFile>(), Array.Empty<string>(), Array.Empty<string>(), new[] { "/c", "echo", "bad", ">", marker }));
        Assert.False(File.Exists(marker));
        await using var lease = WindowsNativeLaunchLease.Start(WindowsNativeLaunchProfile.TrustedWindowsSystemAndCooperativeNativeAot, fixture.Staging, good,
            Array.Empty<WindowsSnapshotFile>(), Array.Empty<string>(), TrustedModules(new[] { fixture.Image }), new[] { "/c", "exit 1" });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        Assert.Equal(1u, await lease.WaitForExitAsync(timeout.Token));
    }

    [Fact]
    public void MissingReplacedOrMisclassifiedApplicationModuleNeverStarts()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new Fixture();
        File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), fixture.Image, overwrite: true);
        var image = new WindowsSnapshotFile(fixture.Image, SHA256.HashData(File.ReadAllBytes(fixture.Image)));
        var expected = new WindowsSnapshotFile(fixture.Dependency, SHA256.HashData(File.ReadAllBytes(fixture.Dependency)));
        var marker = Path.Combine(fixture.Root, "unexpected.txt");
        var arguments = new[] { "/c", "echo", "bad", ">", marker };
        Assert.Throws<ArgumentException>(() => WindowsNativeLaunchLease.Start(
            WindowsNativeLaunchProfile.TrustedWindowsSystemAndCooperativeNativeAot, fixture.Staging, image,
            Array.Empty<WindowsSnapshotFile>(), new[] { "native.dll" }, new[] { "NATIVE.DLL" }, arguments));
        Assert.Throws<ArgumentException>(() => WindowsNativeLaunchLease.Start(
            WindowsNativeLaunchProfile.TrustedWindowsSystemAndCooperativeNativeAot, fixture.Staging, image,
            new[] { expected }, new[] { "native.dll" }, new[] { "NATIVE.DLL" }, arguments));
        Assert.Throws<ArgumentException>(() => WindowsNativeLaunchLease.Start(
            WindowsNativeLaunchProfile.TrustedWindowsSystemAndCooperativeNativeAot, fixture.Staging, image,
            new[] { expected }, new[] { @"sub\native.dll" }, Array.Empty<string>(), arguments));
        File.WriteAllText(fixture.Dependency, "replacement");
        Assert.Throws<InvalidDataException>(() => WindowsNativeLaunchLease.Start(
            WindowsNativeLaunchProfile.TrustedWindowsSystemAndCooperativeNativeAot, fixture.Staging, image,
            new[] { expected }, new[] { "NATIVE.DLL" }, Array.Empty<string>(), arguments));
        Assert.False(File.Exists(marker));
    }

    [Fact]
    public void TruncatedNativeImageNeverStarts()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new Fixture();
        File.WriteAllBytes(fixture.Image, new byte[] { 0x4d, 0x5a });
        var image = new WindowsSnapshotFile(fixture.Image, SHA256.HashData(File.ReadAllBytes(fixture.Image)));
        var marker = Path.Combine(fixture.Root, "unexpected.txt");
        Assert.ThrowsAny<Exception>(() => WindowsNativeLaunchLease.Start(
            WindowsNativeLaunchProfile.TrustedWindowsSystemAndCooperativeNativeAot, fixture.Staging, image,
            Array.Empty<WindowsSnapshotFile>(), Array.Empty<string>(), Array.Empty<string>(),
            new[] { "/c", "echo", "bad", ">", marker }));
        Assert.False(File.Exists(marker));
    }

    [NativeAotTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SnapshotJobStopsChildOnOwnerExit(bool killOwner)
    {
        if (!OperatingSystem.IsWindows()) return;
        var native = Environment.GetEnvironmentVariable("GU_WINDOWS_SNAPSHOT_PEER")
            ?? throw new InvalidOperationException("The Native AOT snapshot peer is required.");
        using var fixture = new Fixture();
        File.Copy(native, fixture.Image, overwrite: true);
        var sqlite = Path.Combine(fixture.Root, "e_sqlite3.dll");
        File.Copy(Path.Combine(Path.GetDirectoryName(native)!, "e_sqlite3.dll"), sqlite);
        var marker = Path.Combine(fixture.Root, "child.txt");
        var start = new ProcessStartInfo(native)
        {
            UseShellExecute = false, RedirectStandardInput = true, RedirectStandardError = true, CreateNoWindow = true
        };
        foreach (var argument in new[] { "--snapshot-owner-probe", fixture.Staging, fixture.Image, sqlite, marker })
            start.ArgumentList.Add(argument);
        foreach (var module in TrustedModules(new[] { fixture.Image, sqlite })) start.ArgumentList.Add(module);
        using var owner = Process.Start(start) ?? throw new InvalidOperationException("Could not start the owner.");
        var errors = owner.StandardError.ReadToEndAsync();
        Process? child = null;
        try
        {
            for (var i = 0; i < 600 && !File.Exists(marker + ".ready") && !File.Exists(marker + ".error") && !owner.HasExited; i++) await Task.Delay(50);
            Assert.True(File.Exists(marker + ".ready"), File.Exists(marker + ".error") ? File.ReadAllText(marker + ".error") :
                owner.HasExited ? await errors : "Child did not start.");
            var lines = File.ReadAllLines(marker);
            child = Process.GetProcessById(int.Parse(lines[1]));
            Assert.False(child.HasExited);
            if (killOwner) owner.Kill();
            else await owner.StandardInput.WriteLineAsync("release");
            await owner.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            if (!killOwner) Assert.True(owner.ExitCode == 0, await errors);
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally
        {
            if (!owner.HasExited) owner.Kill();
            await owner.WaitForExitAsync();
            if (child != null)
            {
                if (!child.HasExited) child.Kill();
                await child.WaitForExitAsync();
                child.Dispose();
            }
        }
    }

    [NativeAotFact]
    public async Task SnapshotLaunchInheritsNoUnlistedHandles()
    {
        if (!OperatingSystem.IsWindows()) return;
        var native = Environment.GetEnvironmentVariable("GU_WINDOWS_SNAPSHOT_PEER")
            ?? throw new InvalidOperationException("The Native AOT snapshot peer is required.");
        using var fixture = new Fixture();
        File.Copy(native, fixture.Image, overwrite: true);
        var marker = Path.Combine(fixture.Root, "inherited.txt");
        var attributes = new SecurityAttributes { Length = Marshal.SizeOf<SecurityAttributes>(), InheritHandle = true };
        using var first = CreateInheritableFile(fixture.Dependency, 0x80000000, 7, ref attributes, 3, 0x80, IntPtr.Zero);
        using var second = CreateInheritableFile(fixture.Dependency, 0x80000000, 7, ref attributes, 3, 0x80, IntPtr.Zero);
        if (first.IsInvalid || second.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        var image = new WindowsSnapshotFile(fixture.Image, SHA256.HashData(File.ReadAllBytes(fixture.Image)));
        await using var lease = WindowsNativeLaunchLease.Start(WindowsNativeLaunchProfile.TrustedWindowsSystemAndCooperativeNativeAot, fixture.Staging, image,
            Array.Empty<WindowsSnapshotFile>(), Array.Empty<string>(), TrustedModules(new[] { fixture.Image }), new[] { "--handle-list-probe",
                first.DangerousGetHandle().ToInt64().ToString(),
                second.DangerousGetHandle().ToInt64().ToString(), marker });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Assert.Equal(0u, await lease.WaitForExitAsync(timeout.Token));
        var lines = File.ReadAllLines(marker);
        Assert.Equal(2, lines.Length);
        Assert.DoesNotContain(lines, path => path.EndsWith(fixture.Dependency, StringComparison.OrdinalIgnoreCase));
    }

    [NativeAotFact]
    public async Task AnotherProcessCannotReplaceHeldImageOrAncestor()
    {
        if (!OperatingSystem.IsWindows()) return;
        var native = Environment.GetEnvironmentVariable("GU_WINDOWS_SNAPSHOT_PEER")
            ?? throw new InvalidOperationException("The Native AOT snapshot peer is required.");
        using var fixture = new Fixture();
        using var lease = fixture.Create();
        var marker = Path.Combine(fixture.Root, "conflicts.txt");
        var start = new ProcessStartInfo(native)
        {
            UseShellExecute = false, RedirectStandardError = true, CreateNoWindow = true
        };
        foreach (var argument in new[] { "--snapshot-conflict-probe", lease.ImagePath, fixture.Staging, marker })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the probe.");
        var errors = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            Assert.True(process.ExitCode == 0, await errors);
            Assert.Equal(new[] { "32", "32" }, File.ReadAllLines(marker));
        }

        finally
        {
            if (!process.HasExited) process.Kill();
            await process.WaitForExitAsync();
        }
    }

    [NativeAotTheory]
    [InlineData("write")]
    [InlineData("delete")]
    public async Task PriorCrossProcessWriteOrDeleteHandleRejectsSnapshot(string access)
    {
        var native = Environment.GetEnvironmentVariable("GU_WINDOWS_SNAPSHOT_PEER")
            ?? throw new InvalidOperationException("The Native AOT snapshot peer is required.");
        using var fixture = new Fixture();
        var image = new WindowsSnapshotFile(fixture.Image, SHA256.HashData(File.ReadAllBytes(fixture.Image)));
        var start = new ProcessStartInfo(native)
        {
            UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true,
            RedirectStandardError = true, CreateNoWindow = true
        };
        foreach (var argument in new[] { "--hold-file-probe", fixture.Image, access })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the probe.");
        var errors = process.StandardError.ReadToEndAsync();
        try
        {
            var ready = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30));
            Assert.True(ready == "ready", ready ?? await errors);
            var failure = Assert.Throws<Win32Exception>(() => WindowsImageSnapshotLease.Create(
                true, fixture.Staging, image, Array.Empty<WindowsSnapshotFile>()));
            Assert.Equal(32, failure.NativeErrorCode);
        }
        finally
        {
            if (!process.HasExited) await process.StandardInput.WriteLineAsync("release");
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)); }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    await process.WaitForExitAsync();
                }
            }
        }
    }

    [NativeAotFact]
    public async Task NativePeerReceivesExactArguments()
    {
        var native = Environment.GetEnvironmentVariable("GU_WINDOWS_SNAPSHOT_PEER")
            ?? throw new InvalidOperationException("The Native AOT snapshot peer is required.");
        using var fixture = new Fixture();
        File.Copy(native, fixture.Image, overwrite: true);
        var marker = Path.Combine(fixture.Root, "arguments.txt");
        var values = new[] { "", "space value", "quote\"value", "slashes\\\\", "trailing space ", "unicode \u6d4b\u8bd5" };
        var arguments = new[] { "--argument-probe", marker }.Concat(values).ToArray();
        var image = new WindowsSnapshotFile(fixture.Image, SHA256.HashData(File.ReadAllBytes(fixture.Image)));
        await using var lease = WindowsNativeLaunchLease.Start(WindowsNativeLaunchProfile.TrustedWindowsSystemAndCooperativeNativeAot,
            fixture.Staging, image, Array.Empty<WindowsSnapshotFile>(), Array.Empty<string>(), TrustedModules(new[] { fixture.Image }), arguments);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Assert.Equal(0u, await lease.WaitForExitAsync(timeout.Token));
        Assert.Equal(values, File.ReadAllLines(marker).Select(line => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(line))));
    }

    private static string[] TrustedModules(IEnumerable<string> paths)
    {
        var files = paths.ToArray();
        var application = files.Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var modules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in files)
        {
            using var stream = File.OpenRead(path);
            using var pe = new PEReader(stream);
            var header = pe.PEHeaders.PEHeader ?? throw new InvalidDataException("Invalid PE image.");
            void ReadTable(DirectoryEntry table, bool delay)
            {
                if (table.RelativeVirtualAddress == 0) return;
                var reader = pe.GetSectionData(table.RelativeVirtualAddress).GetReader(0, table.Size);
                var size = delay ? 32 : 20;
                while (reader.RemainingBytes >= size)
                {
                    var fields = new uint[delay ? 8 : 5];
                    for (var i = 0; i < fields.Length; i++) fields[i] = reader.ReadUInt32();
                    if (fields.All(value => value == 0)) return;
                    var address = fields[delay ? 1 : 3];
                    var name = pe.GetSectionData(checked((int)address)).GetReader();
                    var bytes = new List<byte>();
                    byte value;
                    while ((value = name.ReadByte()) != 0) bytes.Add(value);
                    var module = System.Text.Encoding.ASCII.GetString(bytes.ToArray());
                    if (!application.Contains(module)) modules.Add(module);
                }
                throw new InvalidDataException("Invalid test PE import table.");
            }
            ReadTable(header.ImportTableDirectory, delay: false);
            ReadTable(header.DelayImportTableDirectory, delay: true);
        }
        return modules.ToArray();
    }

    public sealed class NativeAotFactAttribute : FactAttribute
    {
        public NativeAotFactAttribute()
        {
            if (!OperatingSystem.IsWindows() || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GU_WINDOWS_SNAPSHOT_PEER")))
                Skip = "The Windows Native AOT snapshot peer is required.";
        }
    }

    public sealed class NativeAotTheoryAttribute : TheoryAttribute
    {
        public NativeAotTheoryAttribute()
        {
            if (!OperatingSystem.IsWindows() || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GU_WINDOWS_SNAPSHOT_PEER")))
                Skip = "The Windows Native AOT snapshot peer is required.";
        }
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

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public int Length;
        public IntPtr SecurityDescriptor;
        [MarshalAs(UnmanagedType.Bool)] public bool InheritHandle;
    }
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateInheritableFile(string path, uint access, uint share, ref SecurityAttributes security,
        uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(SafeProcessHandle process, uint exitCode);
}
