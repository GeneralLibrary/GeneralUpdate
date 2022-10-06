using GeneralUpdate.Zip.Events;
using System;
using System.IO;
using System.Text;

namespace GeneralUpdate.Zip.Factory
{
    public abstract class BaseCompress : IDisposable, IOperation
    {
        #region Constructors

        public BaseCompress() => SOLUTION_BASE_PATH = AppDomain.CurrentDomain.BaseDirectory;

        #endregion Constructors

        #region Public Properties

        protected string SOLUTION_BASE_PATH { get; set; }
        protected string SOURSE_PATH { get; set; }
        protected string COMPRESS_NAME { get; set; }

        public delegate void CompleteEventHandler(object sender, BaseCompleteEventArgs e);

        public event CompleteEventHandler Completed;

        public delegate void UnZipProgressEventHandler(object sender, BaseUnZipProgressEventArgs e);

        public event UnZipProgressEventHandler UnZipProgress;

        public delegate void CompressProgressEventHandler(object sender, BaseCompressProgressEventArgs e);

        public event CompressProgressEventHandler CompressProgress;

        #endregion Public Properties

        #region Public Methods

        public void OnCompletedEventHandler(object sender, BaseCompleteEventArgs e)
        {
            if (Completed != null) Completed(sender, e);
        }

        public void OnCompressProgressEventHandler(object sender, BaseCompressProgressEventArgs e)
        {
            if (CompressProgress != null) CompressProgress(sender, e);
        }

        public void OnUnZipProgressEventHandler(object sender, BaseUnZipProgressEventArgs e)
        {
            if (UnZipProgress != null) UnZipProgress(sender, e);
        }

        public void Verifypath(string soursePath, string destinationPath)
        {
            if (string.IsNullOrWhiteSpace(soursePath) || string.IsNullOrWhiteSpace(destinationPath)) throw new ArgumentNullException("'Sourse path' or 'Destination path' Is null or empty.");
            if (!Directory.Exists(destinationPath)) throw new Exception("The destination directory does not exist !");
        }

        public void Dispose()
        {
            if (string.IsNullOrWhiteSpace(SOURSE_PATH)) throw new ArgumentNullException(nameof(SOURSE_PATH));
            Directory.Delete(SOURSE_PATH, true);
        }

        public abstract void Configs(string name ,string sourcePath, string destinationPath, Encoding encoding, bool includeBaseDirectory = false);

        public abstract bool CreatZip();

        public abstract bool UnZip();

        #endregion Public Methods
    }
}