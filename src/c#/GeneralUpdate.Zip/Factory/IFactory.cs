using System.Text;

namespace GeneralUpdate.Zip.Factory
{
    public interface IFactory
    {
        IFactory CreatefOperate(OperationType type,string name, string sourcePath, string destinationPath, bool includeBaseDirectory = false, Encoding encoding = null);

        /// <summary>
        /// Create a compressed package.
        /// </summary>
        /// <returns></returns>
        IFactory CreatZip();

        /// <summary>
        /// unzip
        /// </summary>
        /// <returns></returns>
        IFactory UnZip();
    }
}