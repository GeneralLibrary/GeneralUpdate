#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

namespace GeneralUpdate.Core.Security;

internal static class WindowsNativeImportPolicy
{
    internal static void ValidateManifest(IReadOnlyList<WindowsSnapshotFile> dependencies,
        IReadOnlyList<string> applicationModules, IReadOnlyList<string> systemModules)
    {
        var application = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in applicationModules)
            if (!IsModuleName(name) || !application.Add(name))
                throw new ArgumentException("Application module names must be distinct DLL names.", nameof(applicationModules));
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dependency in dependencies)
            if (!files.Add(Path.GetFileName(dependency.SourcePath)))
                throw new ArgumentException("Application dependency file names must be distinct.", nameof(dependencies));
        if (!files.SetEquals(application))
            throw new ArgumentException("Every application module must have exactly one authorized dependency file.", nameof(dependencies));
        foreach (var name in systemModules)
            if (application.Contains(name))
                throw new ArgumentException("An application module cannot be classified as a system module.", nameof(systemModules));
    }

    internal static void Validate(string directory, IReadOnlyList<WindowsSnapshotFile> files,
        IReadOnlyList<string> systemModules)
    {
        var application = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files) application.Add(Path.GetFileName(file.SourcePath));
        var system = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in systemModules)
        {
            if (!IsModuleName(name) || !system.Add(name) || application.Contains(name))
                throw new ArgumentException("System module names must be distinct DLL names and cannot name an application file.", nameof(systemModules));
        }
        foreach (var file in files)
        {
            var path = Path.Combine(directory, Path.GetFileName(file.SourcePath));
            using var stream = File.OpenRead(path);
            using var pe = new PEReader(stream);
            var header = pe.PEHeaders.PEHeader;
            if (header == null || pe.PEHeaders.CorHeader != null)
                throw new InvalidDataException("A snapshot file is not a native PE image.");
            RejectForwardedExports(pe, header.ExportTableDirectory);
            foreach (var name in ReadImports(pe, header.ImportTableDirectory))
                if (!system.Contains(name) && !application.Contains(name))
                    throw new InvalidDataException("A native import is not in the authorized application or system module lists: " + name);
            foreach (var name in ReadDelayImports(pe, header.DelayImportTableDirectory))
                if (!system.Contains(name) && !application.Contains(name))
                    throw new InvalidDataException("A delay-loaded import is not in the authorized application or system module lists: " + name);
        }
    }

    private static IEnumerable<string> ReadDelayImports(PEReader pe, DirectoryEntry imports)
    {
        if (imports.RelativeVirtualAddress == 0 && imports.Size == 0) yield break;
        if (imports.RelativeVirtualAddress <= 0 || imports.Size < 32)
            throw new InvalidDataException("The delay import table is invalid.");
        var reader = pe.GetSectionData(imports.RelativeVirtualAddress).GetReader(0, imports.Size);
        for (var i = 0; i < 4096 && reader.RemainingBytes >= 32; i++)
        {
            var attributes = reader.ReadUInt32();
            var nameAddress = reader.ReadUInt32();
            var rest = 0u;
            for (var field = 0; field < 6; field++) rest |= reader.ReadUInt32();
            if ((attributes | nameAddress | rest) == 0) yield break;
            if (attributes != 1 || nameAddress == 0 || nameAddress > int.MaxValue)
                throw new InvalidDataException("A delay import descriptor is unsupported.");
            var name = ReadModuleName(pe.GetSectionData((int)nameAddress).GetReader());
            if (!IsModuleName(name)) throw new InvalidDataException("A delay import is not a supported DLL name.");
            yield return name;
        }
        throw new InvalidDataException("The delay import table is not terminated or is too large.");
    }

    private static IEnumerable<string> ReadImports(PEReader pe, DirectoryEntry imports)
    {
        if (imports.RelativeVirtualAddress == 0 && imports.Size == 0) yield break;
        if (imports.RelativeVirtualAddress <= 0 || imports.Size < 20)
            throw new InvalidDataException("The native import table is invalid.");
        var reader = pe.GetSectionData(imports.RelativeVirtualAddress).GetReader(0, imports.Size);
        for (var i = 0; i < 4096 && reader.RemainingBytes >= 20; i++)
        {
            var originalThunk = reader.ReadUInt32();
            var timestamp = reader.ReadUInt32();
            var forwarder = reader.ReadUInt32();
            var nameAddress = reader.ReadUInt32();
            var thunk = reader.ReadUInt32();
            if ((originalThunk | timestamp | forwarder | nameAddress | thunk) == 0) yield break;
            if (nameAddress == 0 || nameAddress > int.MaxValue) throw new InvalidDataException("A native import name is invalid.");
            var name = ReadModuleName(pe.GetSectionData((int)nameAddress).GetReader());
            if (!IsModuleName(name)) throw new InvalidDataException("A native import is not a supported DLL name.");
            yield return name;
        }
        throw new InvalidDataException("The native import table is not terminated or is too large.");
    }

    private static string ReadModuleName(BlobReader reader)
    {
        var bytes = new List<byte>();
        while (reader.RemainingBytes > 0 && bytes.Count < 260)
        {
            var value = reader.ReadByte();
            if (value == 0) return Encoding.ASCII.GetString(bytes.ToArray());
            if (value > 0x7f) throw new InvalidDataException("A native import name is not ASCII.");
            bytes.Add(value);
        }
        throw new InvalidDataException("A native import name is not terminated or is too long.");
    }

    private static bool IsModuleName(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > 259 || name[0] == '.' || name.Contains("..", StringComparison.Ordinal) ||
            !name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) return false;
        foreach (var c in name)
            if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') &&
                !(c >= '0' && c <= '9') && c != '_' && c != '-' && c != '.')
                return false;
        return true;
    }

    private static void RejectForwardedExports(PEReader pe, DirectoryEntry exports)
    {
        if (exports.RelativeVirtualAddress == 0 && exports.Size == 0) return;
        if (exports.RelativeVirtualAddress <= 0 || exports.Size < 40)
            throw new InvalidDataException("The native export table is invalid.");
        var reader = pe.GetSectionData(exports.RelativeVirtualAddress).GetReader(0, exports.Size);
        reader.Offset = 20;
        var count = reader.ReadUInt32();
        reader.Offset = 28;
        var functions = reader.ReadUInt32();
        if (count > 65536 || (count != 0 && (functions == 0 || functions > int.MaxValue)))
            throw new InvalidDataException("The native export address table is invalid.");
        if (count == 0) return;
        var addresses = pe.GetSectionData((int)functions).GetReader(0, checked((int)count * 4));
        var start = (uint)exports.RelativeVirtualAddress;
        var end = (ulong)start + (uint)exports.Size;
        for (var i = 0; i < count; i++)
        {
            var address = addresses.ReadUInt32();
            if (address >= start && address < end)
                throw new InvalidDataException("Forwarded application exports are not supported.");
        }
    }
}
#endif
