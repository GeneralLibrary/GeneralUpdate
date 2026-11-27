using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace CoreTest.Ipc;

/// <summary>Exercises atomic private-directory creation, not a protected updater launcher.</summary>
public sealed class WindowsAtomicSnapshotExperimentTests
{
    [Fact]
    public void AtomicCreateReturnsLockedPrivateObjectAndRejectsExistingName()
    {
        if (!OperatingSystem.IsWindows()) return;
        RunExperiment();
    }

    [SupportedOSPlatform("windows")]
    private static void RunExperiment()
    {
        var root = Path.Combine(Path.GetTempPath(), "gu-atomic-directory-" + Guid.NewGuid().ToString("N"));
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
            using var parent = CreateFile(root, 0x80000000, 1, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
            if (parent.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
            using var snapshot = CreateDirectoryRelative(parent, "snapshot", security, out var result, out var information);
            Assert.Equal(0, result);
            Assert.Equal(2, information.ToInt64());
            Assert.False(snapshot.IsInvalid);
            var path = Path.Combine(root, "snapshot");
            var actual = new DirectoryInfo(path).GetAccessControl();
            Assert.True(actual.AreAccessRulesProtected);
            Assert.Equal(identity, actual.GetOwner(typeof(SecurityIdentifier)));
            Assert.All(actual.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>(),
                rule => Assert.Equal(identity, rule.IdentityReference));
            Assert.Throws<IOException>(() => Directory.Move(path, path + ".moved"));
            using var collision = CreateDirectoryRelative(parent, "snapshot", security, out var collisionResult, out _);
            Assert.Equal(unchecked((int)0xc0000035), collisionResult);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [SupportedOSPlatform("windows")]
    private static SafeFileHandle CreateDirectoryRelative(SafeFileHandle parent, string name, DirectorySecurity security,
        out int result, out IntPtr information)
    {
        var nameBuffer = Marshal.StringToHGlobalUni(name);
        var namePointer = Marshal.AllocHGlobal(Marshal.SizeOf<UnicodeString>());
        var descriptorBytes = security.GetSecurityDescriptorBinaryForm();
        var descriptor = Marshal.AllocHGlobal(descriptorBytes.Length);
        try
        {
            Marshal.Copy(descriptorBytes, 0, descriptor, descriptorBytes.Length);
            var unicode = new UnicodeString
            {
                Length = checked((ushort)(name.Length * 2)),
                MaximumLength = checked((ushort)((name.Length + 1) * 2)),
                Buffer = nameBuffer
            };
            Marshal.StructureToPtr(unicode, namePointer, false);
            var attributes = new ObjectAttributes
            {
                Length = Marshal.SizeOf<ObjectAttributes>(),
                RootDirectory = parent.DangerousGetHandle(),
                ObjectName = namePointer,
                Attributes = 0x40,
                SecurityDescriptor = descriptor
            };
            result = NtCreateFile(out var handle, 0x00120081, ref attributes, out var status,
                IntPtr.Zero, 0x80, 1, 2, 0x00200021, IntPtr.Zero, 0);
            information = status.Information;
            return handle;
        }
        finally
        {
            Marshal.FreeHGlobal(descriptor);
            Marshal.FreeHGlobal(namePointer);
            Marshal.FreeHGlobal(nameBuffer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString
    {
        public ushort Length, MaximumLength;
        public IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ObjectAttributes
    {
        public int Length;
        public IntPtr RootDirectory, ObjectName;
        public uint Attributes;
        public IntPtr SecurityDescriptor, SecurityQualityOfService;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoStatusBlock
    {
        public IntPtr Status, Information;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security,
        uint creation, uint flags, IntPtr template);

    [DllImport("ntdll.dll")]
    private static extern int NtCreateFile(out SafeFileHandle handle, uint access, ref ObjectAttributes attributes,
        out IoStatusBlock status, IntPtr allocationSize, uint fileAttributes, uint share, uint disposition,
        uint options, IntPtr extendedAttributes, uint extendedAttributesLength);
}
