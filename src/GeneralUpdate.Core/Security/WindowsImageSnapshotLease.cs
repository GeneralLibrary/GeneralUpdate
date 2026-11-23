// This snapshot is part of the unfinished launch experiment, not a production API.
#if NET8_0_OR_GREATER && WINDOWS_SNAPSHOT_LAUNCH_EXPERIMENT
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace GeneralUpdate.Core.Security;

/// <summary>A host-authorized file to copy into a Windows execution snapshot.</summary>
public sealed class WindowsSnapshotFile
{
    public WindowsSnapshotFile(string sourcePath, byte[] sha256)
    {
        SourcePath = sourcePath ?? throw new ArgumentNullException(nameof(sourcePath));
        if (sha256 == null || sha256.Length != 32) throw new ArgumentException("A SHA256 digest is required.", nameof(sha256));
        Sha256 = (byte[])sha256.Clone();
    }

    public string SourcePath { get; }
    internal byte[] Sha256 { get; }
}

/// <summary>
/// Keeps a private, hash-verified Windows file snapshot and its ancestor handles open.
/// This object does not launch or authenticate a process.
/// </summary>
public sealed class WindowsImageSnapshotLease : IDisposable
{
    private readonly List<SafeFileHandle> _handles;
    private readonly SafeFileHandle _parent;
    private readonly SafeFileHandle _directory;
    private readonly string _directoryName;
    private readonly List<(string Name, SafeFileHandle Handle)> _files;
    private int _disposed;

    private WindowsImageSnapshotLease(string directory, string image, List<SafeFileHandle> handles,
        SafeFileHandle parent, SafeFileHandle directoryHandle, string directoryName,
        List<(string Name, SafeFileHandle Handle)> files)
    {
        DirectoryPath = directory;
        ImagePath = image;
        _handles = handles;
        _parent = parent;
        _directory = directoryHandle;
        _directoryName = directoryName;
        _files = files;
    }

    public string DirectoryPath { get; }
    public string ImagePath { get; }

    /// <summary>
    /// Creates a new protected snapshot. The feature is disabled unless explicitly enabled.
    /// The caller must separately establish a controlled launch and a trusted peer contract.
    /// Cleanup uses relative handles and verifies object identities before deletion.
    /// </summary>
    public static WindowsImageSnapshotLease Create(bool enabled, string snapshotParent,
        WindowsSnapshotFile image, IReadOnlyList<WindowsSnapshotFile> dependencies,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows is required.");
        if (!enabled) throw new InvalidOperationException("Windows image snapshots are disabled.");
        if (snapshotParent == null) throw new ArgumentNullException(nameof(snapshotParent));
        if (image == null) throw new ArgumentNullException(nameof(image));
        if (dependencies == null) throw new ArgumentNullException(nameof(dependencies));
        return CreateWindows(snapshotParent, image, dependencies, cancellationToken);
    }

    [SupportedOSPlatform("windows")]
    private static WindowsImageSnapshotLease CreateWindows(string snapshotParent, WindowsSnapshotFile image,
        IReadOnlyList<WindowsSnapshotFile> dependencies, CancellationToken cancellationToken)
    {
        var user = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("The current user has no SID.");
        var parentPath = ValidatePath(snapshotParent);
        var files = new List<WindowsSnapshotFile> { image };
        files.AddRange(dependencies);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            if (file == null) throw new ArgumentException("A dependency is null.", nameof(dependencies));
            var source = ValidatePath(file.SourcePath);
            var name = Path.GetFileName(source);
            if (!names.Add(name)) throw new ArgumentException("Snapshot file names must be distinct.", nameof(dependencies));
            if (ReferenceEquals(file, image))
            {
                if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("The image must be an executable.", nameof(image));
            }
            else if (!name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Dependencies must be DLL files.", nameof(dependencies));
        }

        var leases = new List<SafeFileHandle>();
        var heldFiles = new List<(string Name, SafeFileHandle Handle)>();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parent = LockAncestors(parentPath, user, leases);
            var directorySecurity = new DirectorySecurity();
            directorySecurity.SetOwner(user);
            directorySecurity.SetAccessRuleProtection(true, false);
            directorySecurity.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None, AccessControlType.Allow));
            var directoryName = "gu-snapshot-" + Guid.NewGuid().ToString("N");
            var snapshot = CreateRelative(parent, directoryName, directorySecurity, directory: true, write: true);
            leases.Add(snapshot);
            ValidateHandle(snapshot, user, directory: true, ancestor: false, privateSnapshot: true);
            var directoryPath = Path.Combine(parentPath, directoryName);

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CopyVerifiedFile(file, snapshot, user, leases, heldFiles, cancellationToken);
            }
            var heldDirectory = NtOpenRelative(parent, directoryName, null, directory: true, write: false, create: false, share: 3);
            try
            {
                if (!GetFileInformationByHandle(snapshot, out var created) ||
                    !GetFileInformationByHandle(heldDirectory, out var held))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (created.VolumeSerialNumber != held.VolumeSerialNumber ||
                    created.FileIndexHigh != held.FileIndexHigh || created.FileIndexLow != held.FileIndexLow)
                    throw new InvalidDataException("The snapshot directory changed during creation.");
                ValidateHandle(heldDirectory, user, directory: true, ancestor: false, privateSnapshot: true);
                leases.Add(heldDirectory);
            }
            catch
            {
                heldDirectory.Dispose();
                throw;
            }
            snapshot.Dispose();
            return new WindowsImageSnapshotLease(directoryPath, Path.Combine(directoryPath, Path.GetFileName(image.SourcePath)),
                leases, parent, heldDirectory, directoryName, heldFiles);
        }
        catch
        {
            for (var i = leases.Count - 1; i >= 0; i--) leases[i].Dispose();
            throw;
        }
    }

    [SupportedOSPlatform("windows")]
    private static void CopyVerifiedFile(WindowsSnapshotFile file, SafeFileHandle snapshot, SecurityIdentifier user,
        List<SafeFileHandle> leases, List<(string Name, SafeFileHandle Handle)> heldFiles, CancellationToken cancellationToken)
    {
        var path = ValidatePath(file.SourcePath);
        var sourceLeases = new List<SafeFileHandle>();
        try
        {
            LockAncestors(Path.GetDirectoryName(path)!, user, sourceLeases);
            using var source = OpenPath(path, directory: false, root: false);
            ValidateHandle(source, user, directory: false, ancestor: false);
            var security = new FileSecurity();
            security.SetOwner(user);
            security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl, AccessControlType.Allow));
            var name = Path.GetFileName(path);
            using (var destination = CreateRelative(snapshot, name, security, directory: false, write: true))
            using (var stream = new FileStream(destination, FileAccess.ReadWrite))
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[81920];
                long offset = 0;
                int count;
                while ((count = RandomAccess.Read(source, buffer, offset)) != 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    hash.AppendData(buffer, 0, count);
                    stream.Write(buffer, 0, count);
                    offset += count;
                }
                if (!CryptographicOperations.FixedTimeEquals(hash.GetHashAndReset(), file.Sha256))
                    throw new InvalidDataException("A source file does not match its authorized SHA256.");
                stream.Flush();
                stream.Position = 0;
                if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(stream), file.Sha256))
                    throw new InvalidDataException("A snapshot file does not match its authorized SHA256.");
            }
            var held = OpenRelative(snapshot, name, directory: false);
            try
            {
                ValidateHandle(held, user, directory: false, ancestor: false, privateSnapshot: true);
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[81920];
                long offset = 0;
                int count;
                while ((count = RandomAccess.Read(held, buffer, offset)) != 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    hash.AppendData(buffer, 0, count);
                    offset += count;
                }
                if (!CryptographicOperations.FixedTimeEquals(hash.GetHashAndReset(), file.Sha256))
                    throw new InvalidDataException("The held snapshot file does not match its authorized SHA256.");
                leases.Add(held);
                heldFiles.Add((name, held));
            }
            catch
            {
                held.Dispose();
                throw;
            }
        }
        finally
        {
            for (var i = sourceLeases.Count - 1; i >= 0; i--) sourceLeases[i].Dispose();
        }
    }

    [SupportedOSPlatform("windows")]
    private static string ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new ArgumentException("An absolute local path is required.", nameof(path));
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full);
        if (root == null || root.Length != 3 || root[1] != ':' || !string.Equals(path.TrimEnd('\\'), full.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A canonical local drive path is required.", nameof(path));
        var drive = new DriveInfo(root);
        if (drive.DriveType != DriveType.Fixed || !string.Equals(drive.DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase))
            throw new PlatformNotSupportedException("A local fixed NTFS volume is required.");
        foreach (var component in full.Substring(root.Length).Split('\\'))
            if (component.Length == 0 || component.IndexOf(':') >= 0 || component.EndsWith(".", StringComparison.Ordinal) || component.EndsWith(" ", StringComparison.Ordinal))
                throw new ArgumentException("The path contains an unsupported component.", nameof(path));
        return full;
    }

    [SupportedOSPlatform("windows")]
    private static SafeFileHandle LockAncestors(string path, SecurityIdentifier user, List<SafeFileHandle> leases)
    {
        var root = Path.GetPathRoot(path)!;
        var current = root;
        var handle = OpenPath(current, directory: true, root: true);
        leases.Add(handle);
        ValidateHandle(handle, user, directory: true, ancestor: true);
        foreach (var component in path.Substring(root.Length).Split('\\'))
        {
            if (component.Length == 0) continue;
            current = Path.Combine(current, component);
            handle = OpenPath(current, directory: true, root: false);
            leases.Add(handle);
            ValidateHandle(handle, user, directory: true, ancestor: true);
        }
        return handle;
    }

    [SupportedOSPlatform("windows")]
    private static SafeFileHandle OpenPath(string path, bool directory, bool root)
    {
        var handle = CreateFile(path, 0x80000000, root ? 7u : directory ? 3u : 1u, IntPtr.Zero, 3,
            0x00200000 | (directory ? 0x02000000u : 0u), IntPtr.Zero);
        if (!handle.IsInvalid) return handle;
        var error = Marshal.GetLastWin32Error();
        handle.Dispose();
        throw new Win32Exception(error, "Unable to acquire a protected file handle.");
    }

    [SupportedOSPlatform("windows")]
    private static void ValidateHandle(SafeFileHandle handle, SecurityIdentifier user, bool directory, bool ancestor,
        bool privateSnapshot = false)
    {
        if (!GetFileInformationByHandle(handle, out var information)) throw new Win32Exception(Marshal.GetLastWin32Error());
        if ((information.FileAttributes & 0x400) != 0 || ((information.FileAttributes & 0x10) != 0) != directory)
            throw new InvalidDataException("A path component is a reparse point or has an unexpected type.");
        var error = GetSecurityInfo(handle, 1, 0x1 | 0x4, out _, out _, out _, out _, out var descriptor);
        if (error != 0) throw new Win32Exception((int)error, "Unable to read an object security descriptor.");
        try
        {
            var bytes = new byte[GetSecurityDescriptorLength(descriptor)];
            Marshal.Copy(descriptor, bytes, 0, bytes.Length);
            var security = new RawSecurityDescriptor(bytes, 0);
            if (security.Owner == null || !IsTrusted(security.Owner, user) ||
                (security.ControlFlags & ControlFlags.DiscretionaryAclPresent) == 0 || security.DiscretionaryAcl == null)
                throw new InvalidDataException("The object owner or DACL is not trusted.");
            if (privateSnapshot && (!security.Owner.Equals(user) ||
                (security.ControlFlags & ControlFlags.DiscretionaryAclProtected) == 0))
                throw new InvalidDataException("The snapshot owner or DACL protection is invalid.");
            const uint namespaceRights = 0x000d0040 | 0x50000000;
            const uint fileWriteRights = namespaceRights | 0x00000116;
            foreach (GenericAce ace in security.DiscretionaryAcl)
            {
                if ((ace.AceFlags & AceFlags.InheritOnly) != 0) continue;
                if (ace is not CommonAce common || common.IsCallback)
                    throw new InvalidDataException("The object has an unsupported access rule.");
                if (common.AceQualifier == AceQualifier.AccessDenied) continue;
                if (common.AceQualifier != AceQualifier.AccessAllowed)
                    throw new InvalidDataException("The object has an unsupported access rule.");
                if (privateSnapshot && !common.SecurityIdentifier.Equals(user))
                    throw new InvalidDataException("A snapshot access rule grants another principal access.");
                if (!IsTrusted(common.SecurityIdentifier, user) &&
                    (unchecked((uint)common.AccessMask) & (ancestor ? namespaceRights : fileWriteRights)) != 0)
                    throw new InvalidDataException("An untrusted principal can modify the object or namespace.");
            }
        }
        finally
        {
            LocalFree(descriptor);
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool IsTrusted(SecurityIdentifier sid, SecurityIdentifier user) =>
        sid.Equals(user) || sid.IsWellKnown(WellKnownSidType.LocalSystemSid) ||
        sid.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid) ||
        sid.Value == "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464";

    [SupportedOSPlatform("windows")]
    private static SafeFileHandle CreateRelative(SafeFileHandle parent, string name, ObjectSecurity security, bool directory, bool write) =>
        NtOpenRelative(parent, name, security.GetSecurityDescriptorBinaryForm(), directory, write, create: true);

    [SupportedOSPlatform("windows")]
    private static SafeFileHandle OpenRelative(SafeFileHandle parent, string name, bool directory) =>
        NtOpenRelative(parent, name, null, directory, write: false, create: false);

    private static SafeFileHandle NtOpenRelative(SafeFileHandle parent, string name, byte[]? security,
        bool directory, bool write, bool create, uint share = 1, bool delete = false)
    {
        var nameBuffer = Marshal.StringToHGlobalUni(name);
        var namePointer = Marshal.AllocHGlobal(Marshal.SizeOf<UnicodeString>());
        var descriptor = security == null ? IntPtr.Zero : Marshal.AllocHGlobal(security.Length);
        try
        {
            if (security != null) Marshal.Copy(security, 0, descriptor, security.Length);
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
            var access = delete ? 0x00110080u : directory ? (write ? 0x00120083u : 0x00120081u) : write ? 0xc0100000u : 0x80100000u;
            var options = 0x00200020u | (directory ? 1u : 0x40u);
            var status = NtCreateFile(out var handle, access, ref attributes, out _, IntPtr.Zero, 0x80, share,
                create ? 2u : 1u, options, IntPtr.Zero, 0);
            if (status == 0 && !handle.IsInvalid) return handle;
            handle.Dispose();
            var error = (int)RtlNtStatusToDosError(status);
            throw new Win32Exception(error, $"Unable to create or open a snapshot object (NTSTATUS 0x{status:x8}, Win32 {error}).");
        }
        finally
        {
            if (descriptor != IntPtr.Zero) Marshal.FreeHGlobal(descriptor);
            Marshal.FreeHGlobal(namePointer);
            Marshal.FreeHGlobal(nameBuffer);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        try
        {
            foreach (var file in _files)
                DeleteVerified(_directory, file.Name, file.Handle, directory: false);
            DeleteVerified(_parent, _directoryName, _directory, directory: true);
        }
        finally
        {
            for (var i = _handles.Count - 1; i >= 0; i--) _handles[i].Dispose();
        }
    }

    private static void DeleteVerified(SafeFileHandle parent, string name, SafeFileHandle original, bool directory)
    {
        using var retained = NtOpenRelative(parent, name, null, directory, write: false, create: false, share: 7);
        RequireSameObject(original, retained);
        original.Dispose();
        using var deleteHandle = NtOpenRelative(parent, name, null, directory, write: false, create: false, share: 3, delete: true);
        RequireSameObject(retained, deleteHandle);
        var disposition = new FileDispositionInformation { DeleteFile = true };
        var status = NtSetInformationFile(deleteHandle, out _, ref disposition, 1, 13);
        if (status != 0)
        {
            var error = (int)RtlNtStatusToDosError(status);
            throw new Win32Exception(error, $"Unable to delete the snapshot object (NTSTATUS 0x{status:x8}, Win32 {error}).");
        }
    }

    private static void RequireSameObject(SafeFileHandle first, SafeFileHandle second)
    {
        if (!GetFileInformationByHandle(first, out var a) || !GetFileInformationByHandle(second, out var b))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        if (a.VolumeSerialNumber != b.VolumeSerialNumber || a.FileIndexHigh != b.FileIndexHigh || a.FileIndexLow != b.FileIndexLow)
            throw new InvalidDataException("A snapshot object changed during cleanup.");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString { public ushort Length, MaximumLength; public IntPtr Buffer; }
    [StructLayout(LayoutKind.Sequential)]
    private struct ObjectAttributes
    {
        public int Length;
        public IntPtr RootDirectory, ObjectName;
        public uint Attributes;
        public IntPtr SecurityDescriptor, SecurityQualityOfService;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct IoStatusBlock { public IntPtr Status, Information; }
    [StructLayout(LayoutKind.Sequential)]
    private struct FileDispositionInformation { [MarshalAs(UnmanagedType.U1)] public bool DeleteFile; }
    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime, LastAccessTime, LastWriteTime;
        public uint VolumeSerialNumber, FileSizeHigh, FileSizeLow, NumberOfLinks, FileIndexHigh, FileIndexLow;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);
    [DllImport("advapi32.dll")]
    private static extern uint GetSecurityInfo(SafeFileHandle handle, int type, uint information, out IntPtr owner,
        out IntPtr group, out IntPtr dacl, out IntPtr sacl, out IntPtr descriptor);
    [DllImport("advapi32.dll")]
    private static extern uint GetSecurityDescriptorLength(IntPtr descriptor);
    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr pointer);
    [DllImport("ntdll.dll")]
    private static extern int NtCreateFile(out SafeFileHandle handle, uint access, ref ObjectAttributes attributes,
        out IoStatusBlock status, IntPtr allocationSize, uint fileAttributes, uint share, uint disposition,
        uint options, IntPtr extendedAttributes, uint extendedAttributesLength);
    [DllImport("ntdll.dll")]
    private static extern int NtSetInformationFile(SafeFileHandle handle, out IoStatusBlock status,
        ref FileDispositionInformation information, uint length, int informationClass);
    [DllImport("ntdll.dll")]
    private static extern uint RtlNtStatusToDosError(int status);
}
#endif
