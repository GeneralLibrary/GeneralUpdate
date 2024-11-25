using System;
using System.Text;
using GeneralUpdate.Common.Shared.Object;

namespace GeneralUpdate.Common.Compress;

public class CompressProvider
{
    private static ICompressionStrategy _compressionStrategy;

    private CompressProvider() { }

    public static void Compress(string compressType,string sourcePath, string destinationPath, bool includeRootDirectory, Encoding encoding)
    {
        _compressionStrategy = GetCompressionStrategy(compressType);
        _compressionStrategy.Compress(sourcePath, destinationPath, includeRootDirectory, encoding);
    }

    public static void Decompress(string compressType, string archivePath, string destinationPath, Encoding encoding)
    {
        _compressionStrategy = GetCompressionStrategy(compressType);
        _compressionStrategy.Decompress(archivePath, destinationPath, encoding);
    }

    private static ICompressionStrategy GetCompressionStrategy(string compressType) => compressType switch
    {
        Format.ZIP => new ZipCompressionStrategy(),
        _ => throw new ArgumentException("Compression format is not supported!")
    };
}