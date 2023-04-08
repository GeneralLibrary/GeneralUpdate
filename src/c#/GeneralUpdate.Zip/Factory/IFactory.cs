using System.Text;

namespace GeneralUpdate.Zip.Factory
{
    public interface IFactory
    {
        IFactory CreateOperate(OperationType type, string name, string sourcePath, string destinationPath, bool includeBaseDirectory = false, Encoding encoding = null);

        /// <summary>
        /// Create a compressed package.
        /// </summary>
        /// <returns></returns>
        IFactory CreateZip();

        /// <summary>
        /// unzip
        /// </summary>
        /// <returns></returns>
        IFactory UnZip();
    }
}