using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Xunit.Abstractions;

namespace CoreTest.Ipc;

/// <summary>
/// An isolated Windows sharing-semantics experiment, not a trusted updater launcher.
/// Only the newly created test directories are checked; their external ancestors and
/// native dependencies are not validated. The process image API returns a path,
/// not a handle to the process's mapped image section.
/// </summary>
public sealed class WindowsImageLockExperimentTests(ITestOutputHelper output)
{
    [Fact]
    public async Task HeldImageAndPrivateDirectoryRejectConcurrentDeployment()
    {
        if (!OperatingSystem.IsWindows()) return;
        await RunWindowsExperiment();
    }

    [SupportedOSPlatform("windows")]
    private async Task RunWindowsExperiment()
    {
        var root = Path.Combine(Path.GetTempPath(), "gu-image-lock-" + Guid.NewGuid().ToString("N"));
        var identity = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("The current user has no SID.");
        var security = new DirectorySecurity();
        security.SetOwner(identity);
        security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new FileSystemAccessRule(identity, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(root).Create(security);

        try
        {
            var actualSecurity = new DirectoryInfo(root).GetAccessControl();
            Assert.True(actualSecurity.AreAccessRulesProtected);
            AssertPrivateAcl(actualSecurity, identity);

            var directory = Path.Combine(root, "image");
            Directory.CreateDirectory(directory);
            var image = Path.Combine(directory, "peer.exe");
            var replacement = Path.Combine(root, "replacement.exe");
            var nativePeer = Environment.GetEnvironmentVariable("GU_WINDOWS_NATIVE_LOCK_PEER");
            var sourceImage = nativePeer ?? Path.Combine(Environment.SystemDirectory, "cmd.exe");
            File.Copy(sourceImage, image);
            File.Copy(image, replacement);
            AssertPrivateAcl(new DirectoryInfo(directory).GetAccessControl(), identity);
            AssertPrivateAcl(new FileInfo(image).GetAccessControl(), identity);

            using (var rootHandle = OpenLocked(root, directory: true))
            using (var directoryHandle = OpenLocked(directory, directory: true))
            using (var imageHandle = OpenLocked(image, directory: false))
            {
                AssertNotReparsePoint(rootHandle);
                AssertNotReparsePoint(directoryHandle);
                AssertNotReparsePoint(imageHandle);
                var expectedIdentity = IdentityOf(imageHandle);
                var expectedHash = SHA256.HashData(File.ReadAllBytes(sourceImage));
                Assert.Equal(expectedHash, HashHandle(imageHandle));
                var beforeStart = await RunDeploymentPeerAsync(image, replacement, directory);
                output.WriteLine("Before peer starts: " + string.Join(", ", beforeStart));
                AssertBlocked(beforeStart);

                var start = new ProcessStartInfo(image)
                {
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                if (nativePeer == null)
                {
                    start.ArgumentList.Add("/d");
                    start.ArgumentList.Add("/q");
                    start.ArgumentList.Add("/c");
                    start.ArgumentList.Add("pause >nul");
                }
                else
                {
                    start.ArgumentList.Add("--image-lock-peer");
                }
                using var peer = Process.Start(start) ?? throw new InvalidOperationException("Native peer did not start.");
                try
                {
                    if (nativePeer != null)
                        Assert.Equal("ready", await peer.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30)));
                    var imagePath = new StringBuilder(32768);
                    var length = imagePath.Capacity;
                    if (!QueryFullProcessImageName(peer.Handle, 0, imagePath, ref length))
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    using var reportedImage = OpenLocked(imagePath.ToString(), directory: false);
                    Assert.Equal(expectedIdentity, IdentityOf(reportedImage));
                    Assert.Equal(expectedHash, HashHandle(reportedImage));
                    Assert.False(peer.HasExited);

                    var results = await RunDeploymentPeerAsync(image, replacement, directory);
                    output.WriteLine("While locked: " + string.Join(", ", results));
                    AssertBlocked(results);
                    Assert.Equal(expectedIdentity, IdentityOf(imageHandle));
                    Assert.Equal(expectedHash, HashHandle(imageHandle));
                }
                finally
                {
                    if (!peer.HasExited) peer.Kill(entireProcessTree: true);
                    await peer.WaitForExitAsync();
                }
            }

            var unlocked = await RunDeploymentPeerAsync(image, replacement, directory);
            output.WriteLine("After release: " + string.Join(", ", unlocked));
            Assert.Equal(4, unlocked.Length);
            foreach (var operation in new[] { "rename-image", "replace-image", "write-image", "rename-directory" })
                Assert.Contains(operation + ":SUCCESS", unlocked);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void AssertPrivateAcl(FileSystemSecurity security, SecurityIdentifier identity)
    {
        Assert.Equal(identity, security.GetOwner(typeof(SecurityIdentifier)));
        var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>().ToArray();
        Assert.NotEmpty(rules);
        Assert.All(rules, rule => Assert.Equal(identity, rule.IdentityReference));
    }

    private static void AssertBlocked(string[] results)
    {
        Assert.Equal(4, results.Length);
        foreach (var operation in new[] { "rename-image", "replace-image", "write-image", "rename-directory" })
            Assert.True(results.Contains(operation + ":32") || results.Contains(operation + ":5"),
                $"Unexpected deployment results: {string.Join(", ", results)}");
    }

    [SupportedOSPlatform("windows")]
    private static async Task<string[]> RunDeploymentPeerAsync(string image, string replacement, string directory)
    {
        var script = """
                        $image = $args[0]
                        $replacement = $args[1]
                        $directory = $args[2]
                        $operations = @(
                            @{ Name = 'rename-image'; Action = {
                                [IO.File]::Move($image, "$image.moved")
                                [IO.File]::Move("$image.moved", $image)
                            } },
                            @{ Name = 'replace-image'; Action = { [IO.File]::Replace($replacement, $image, "$replacement.bak") } },
                            @{ Name = 'write-image'; Action = { [IO.File]::WriteAllText($image, 'changed') } },
                            @{ Name = 'rename-directory'; Action = { [IO.Directory]::Move($directory, "$directory.moved") } }
                        )
                        foreach ($operation in $operations) {
                            try {
                                & $operation.Action
                                Write-Output "$($operation.Name):SUCCESS"
                            } catch {
                                $errorObject = $_.Exception
                                while ($errorObject.InnerException) { $errorObject = $errorObject.InnerException }
                                Write-Output "$($operation.Name):$($errorObject.HResult -band 65535)"
                            }
                        }
                        """;
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(
            "& { " + script + " } '" + image.Replace("'", "''") + "' '" +
            replacement.Replace("'", "''") + "' '" + directory.Replace("'", "''") + "'"));
        var workerStart = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"))
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        workerStart.ArgumentList.Add("-NoProfile");
        workerStart.ArgumentList.Add("-NonInteractive");
        workerStart.ArgumentList.Add("-EncodedCommand");
        workerStart.ArgumentList.Add(encoded);
        using var worker = Process.Start(workerStart) ?? throw new InvalidOperationException("Deployment peer did not start.");
        var outputTask = worker.StandardOutput.ReadToEndAsync();
        var errorTask = worker.StandardError.ReadToEndAsync();
        try
        {
            await worker.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally
        {
            if (!worker.HasExited) worker.Kill(entireProcessTree: true);
        }
        var output = await outputTask;
        var errors = await errorTask;
        Assert.True(worker.ExitCode == 0, errors);
        return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private static byte[] HashHandle(SafeFileHandle handle)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[8192];
        long offset = 0;
        int read;
        while ((read = RandomAccess.Read(handle, buffer, offset)) != 0)
        {
            hash.AppendData(buffer, 0, read);
            offset += read;
        }
        return hash.GetHashAndReset();
    }

    [SupportedOSPlatform("windows")]
    private static SafeFileHandle OpenLocked(string path, bool directory)
    {
        const uint genericRead = 0x80000000;
        const uint fileShareRead = 0x1;
        const uint openExisting = 3;
        const uint openReparsePoint = 0x00200000;
        const uint backupSemantics = 0x02000000;
        var handle = CreateFile(path, genericRead, fileShareRead, IntPtr.Zero,
            openExisting, openReparsePoint | (directory ? backupSemantics : 0), IntPtr.Zero);
        if (!handle.IsInvalid) return handle;
        var error = Marshal.GetLastWin32Error();
        handle.Dispose();
        throw new Win32Exception(error, $"Cannot lock {path}.");
    }

    [SupportedOSPlatform("windows")]
    private static void AssertNotReparsePoint(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandle(handle, out var information))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        Assert.Equal(0u, information.FileAttributes & 0x400u);
    }

    [SupportedOSPlatform("windows")]
    private static (uint Volume, uint High, uint Low) IdentityOf(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandle(handle, out var information))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return (information.VolumeSerialNumber, information.FileIndexHigh, information.FileIndexLow);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security,
        uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);

    [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder path, ref int length);
}
