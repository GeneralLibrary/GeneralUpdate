using GeneralUpdate.Zip.Events;
using GeneralUpdate.Zip.Factory;
using GeneralUpdate.Zip.G7z;
using GeneralUpdate.Zip.GZip;
using System;
using System.Text;

namespace GeneralUpdate.Zip
{
    /// <summary>
    /// The compression factory chooses the compressed package format you want to operate .
    /// </summary>
    public class GeneralZipFactory : IFactory
    {
        private BaseCompress _operation;

        public delegate void CompleteEventHandler(object sender, BaseCompleteEventArgs e);

        public event CompleteEventHandler Completed;

        public delegate void UnZipProgressEventHandler(object sender, BaseUnZipProgressEventArgs e);

        public event UnZipProgressEventHandler UnZipProgress;

        public delegate void CompressProgressEventHandler(object sender, BaseCompressProgressEventArgs e);

        public event CompressProgressEventHandler CompressProgress;

        /// <summary>
        /// Configuring Compression.
        /// </summary>
        /// <param name="type">Enumeration selects the compressed package format to operate on.(OperationType.GZip , OperationType.G7z)</param>
        /// <param name="name">Compressed package Name.</param>
        /// <param name="sourcePath">Source file path.</param>
        /// <param name="destinationPath">The target path.</param>
        /// <param name="includeBaseDirectory">Whether to include the root directory when packing.</param>
        /// <param name="encoding">Compressed package encoding format.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="Exception"></exception>
        public IFactory CreateOperate(OperationType type, string name, string sourcePath, string destinationPath, bool includeBaseDirectory = false, Encoding encoding = null)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(destinationPath))
                throw new ArgumentNullException("The path cannot be empty !");
            try
            {
                switch (type)
                {
                    case OperationType.GZip:
                        _operation = new GeneralZip();
                        _operation.Configs(name, sourcePath, destinationPath, encoding, includeBaseDirectory);
                        break;

                    case OperationType.G7z:
                        _operation = new General7z();
                        _operation.Configs(name, sourcePath, destinationPath, encoding, includeBaseDirectory);
                        break;
                }
                _operation.CompressProgress += OnCompressProgress;
                _operation.UnZipProgress += OnUnZipProgress;
                _operation.Completed += OnCompleted;
            }
            catch (Exception ex)
            {
                throw new Exception($"'CreateOperate' Initialization exception : {ex.Message} .", ex.InnerException);
            }
            return this;
        }

        private void OnCompleted(object sender, BaseCompleteEventArgs e)
        {
            if (Completed != null) Completed(sender, e);
        }

        private void OnUnZipProgress(object sender, BaseUnZipProgressEventArgs e)
        {
            if (UnZipProgress != null) UnZipProgress(sender, e);
        }

        private void OnCompressProgress(object sender, BaseCompressProgressEventArgs e)
        {
            if (CompressProgress != null) CompressProgress(sender, e);
        }

        public IFactory CreateZip()
        {
            try
            {
                _operation.CreatZip();
            }
            catch (Exception ex)
            {
                throw new Exception($"'CreateZip' exception : {ex.Message} .", ex.InnerException);
            }
            return this;
        }

        public IFactory UnZip()
        {
            try
            {
                _operation.UnZip();
            }
            catch (Exception ex)
            {
                throw new Exception($"'CreateOperate' exception : {ex.Message} .", ex.InnerException);
            }
            return this;
        }
    }
}