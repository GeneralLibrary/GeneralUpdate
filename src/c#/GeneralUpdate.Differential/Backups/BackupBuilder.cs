using System;
using System.Collections.Generic;
using System.IO;

namespace GeneralUpdate.Differential.Backups
{
    public class BackupBuilder
    {
        private IList<string> _files;
        private IList<string> _backupFiles;
        private string BACKUP = "_bak";

        public BackupBuilder(IList<string> files) 
        {
            if(files == null) 
                throw new ArgumentNullException(nameof(files));

            _files = files;
            _backupFiles = new List<string>();
        }

        public void Backup() 
        {
            foreach(var file in _files)
            {
                string dirName = Path.GetDirectoryName(file);
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
                string extension = Path.GetExtension(file);
                string newFullPath = Path.Combine(dirName, fileNameWithoutExtension + BACKUP + extension);
                _backupFiles.Add(newFullPath);
                File.Copy(file, newFullPath);
            }
        }

        public void Restore() 
        {
            for (int i = 0; i < _backupFiles.Count; i++)
            {
                string restorePath = _files[i];
                File.Delete(restorePath);
                File.Move(_backupFiles[i], restorePath);
            }
        }

    }
}
