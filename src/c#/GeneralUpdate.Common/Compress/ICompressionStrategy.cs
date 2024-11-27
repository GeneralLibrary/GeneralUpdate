using System.Text;

namespace GeneralUpdate.Common.Compress;

public interface ICompressionStrategy
{
    void Compress(string sourcePath, string destinationPath, bool includeRootDirectory, Encoding encoding);
    void Decompress(string archivePath, string destinationPath, Encoding encoding);
}