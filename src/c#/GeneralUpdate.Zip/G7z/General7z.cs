using GeneralUpdate.Zip.Events;
using GeneralUpdate.Zip.Factory;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Writers;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace GeneralUpdate.Zip.G7z
{
    /// <summary>
    /// Source address : https://github.com/adamhathcock/sharpcompress/blob/master/tests/SharpCompress.Test/WriterTests.cs
    /// Author : Adam hathcock .
    /// Tribute to the original author .
    /// Secondary developer : Juster Z
    /// </summary>
    public class General7z : BaseCompress
    {
        private string _destinationPath;
        private Encoding _encoding;
        private int _unZipCount = 0;
        private int _unzipTotalCount = 0;

        public override bool CreatZip()
        {
            try
            {
                using (Stream stream = File.OpenWrite(SOURSE_PATH))
                {
                    WriterOptions writerOptions = new WriterOptions(CompressionType.Unknown) { LeaveStreamOpen = true };
                    writerOptions.ArchiveEncoding.Default = _encoding;
                    using (var writer = WriterFactory.Open(stream, ArchiveType.SevenZip, writerOptions))
                    {
                        writer.WriteAll(SOLUTION_BASE_PATH, "*", SearchOption.AllDirectories);
                    }
                }
                using (Stream stream = File.OpenRead(_destinationPath))
                {
                    ReaderOptions readerOptions = new ReaderOptions();
                    readerOptions.ArchiveEncoding.Default = _encoding;
                    using (var reader = ReaderFactory.Open(NonDisposingStream.Create(stream), readerOptions))
                    {
                        reader.WriteAllToDirectory(SOLUTION_BASE_PATH, new ExtractionOptions() { ExtractFullPath = true });
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message, ex.InnerException);
            }
        }

        public override bool UnZip()
        {
            bool isComplete = false;
            try
            {
                ReaderOptions readerOptions = new ReaderOptions();
                readerOptions.ArchiveEncoding.Default = _encoding;
                using (var archive = ArchiveFactory.Open(SOURSE_PATH, readerOptions))
                {
                    var entries = archive.Entries;
                    _unzipTotalCount = entries.Count();
                    archive.FilePartExtractionBegin += OnFilePartExtractionBegin;
                    foreach (var entry in entries.Where(entry => !entry.IsDirectory))
                    {
                        _unZipCount++;
                        entry.WriteToDirectory(_destinationPath, new ExtractionOptions() { ExtractFullPath = true, Overwrite = true });
                    }
                    isComplete = archive.IsComplete;
                }
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                OnCompletedEventHandler(this, new BaseCompleteEventArgs(isComplete));
            }
            return isComplete;
        }

        private void OnFilePartExtractionBegin(object sender, FilePartExtractionBeginEventArgs e)
        {
            OnUnZipProgressEventHandler(sender, new BaseUnZipProgressEventArgs() { Size = e.Size, Name = e.Name, Index = _unZipCount, Count = _unzipTotalCount, Path = Path.Combine(_destinationPath, e.Name) });
        }

        public override void Configs(string name, string sourcePath, string destinationPath, Encoding encoding, bool includeBaseDirectory = false)
        {
            SOURSE_PATH = sourcePath;
            COMPRESS_NAME = $"{name}.7z";
            _destinationPath = destinationPath ?? SOLUTION_BASE_PATH;
            _encoding = encoding ?? Encoding.Default;
            Verifypath(sourcePath, destinationPath);
        }
    }
}