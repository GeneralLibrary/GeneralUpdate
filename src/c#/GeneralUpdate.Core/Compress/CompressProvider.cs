using System;
using System.Text;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.Compress;

public class CompressProvider
{
    private CompressProvider() { }

    public static void Compress(string compressType, string sourcePath, string destinationPath, bool includeRootDirectory, Encoding encoding)
    {
        var strategy = GetCompressionStrategy(compressType);
        strategy.Compress(sourcePath, destinationPath, includeRootDirectory, encoding);
    }

    public static void Decompress(string compressType, string archivePath, string destinationPath, Encoding encoding)
    {
        var strategy = GetCompressionStrategy(compressType);
        strategy.Decompress(archivePath, destinationPath, encoding);
    }

    private static ICompressionStrategy GetCompressionStrategy(string compressType) => compressType switch
    {
        Format.ZIP => new ZipCompressionStrategy(),
        _ => throw new ArgumentException("Compression format is not supported!")
    };
}