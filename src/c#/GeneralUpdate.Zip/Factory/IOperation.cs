using GeneralUpdate.Zip.Events;
using System.Text;

namespace GeneralUpdate.Zip.Factory
{
    public interface IOperation
    {
        /// <summary>
        /// Configure compression parameters.
        /// </summary>
        /// <param name="name">packet name.</param>
        /// <param name="sourcePath">path to be packaged.</param>
        /// <param name="destinationPath">unzip path.</param>
        /// <param name="encoding">Compressed Content Coding Format.</param>
        /// <param name="includeBaseDirectory">Whether to include the root directory when packaging.</param>
        void Configs(string name,string sourcePath, string destinationPath, Encoding encoding, bool includeBaseDirectory = false);

        /// <summary>
        /// Create a compressed package.
        /// </summary>
        /// <returns></returns>
        bool CreatZip();

        /// <summary>
        /// unzip
        /// </summary>
        /// <returns></returns>
        bool UnZip();

        void OnCompressProgressEventHandler(object sender, BaseCompressProgressEventArgs e);

        void OnUnZipProgressEventHandler(object sender, BaseUnZipProgressEventArgs e);
    }
}