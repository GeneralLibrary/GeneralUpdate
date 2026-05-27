using System;
using System.Text;
using GeneralUpdate.Core.Configuration;

namespace GeneralUpdate.Core.Compress;

public class CompressProvider
{
    private CompressProvider() { }

    public static void Compress(Format compressType, string sourcePath, string destinationPath, bool includeRootDirectory, Encoding encoding)
    {
        var strategy = GetCompressionStrategy(compressType);
        strategy.Compress(sourcePath, destinationPath, includeRootDirectory, encoding);
    }

    public static void Decompress(Format compressType, string archivePath, string destinationPath, Encoding encoding)
    {
        var strategy = GetCompressionStrategy(compressType);
        strategy.Decompress(archivePath, destinationPath, encoding);
    }

    private static ICompressionStrategy GetCompressionStrategy(Format compressType) => compressType switch
    {
        Format.Zip => new ZipCompressionStrategy(),
        _ => throw new ArgumentException("Compression format is not supported!")
    };
}