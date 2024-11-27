using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeneralUpdate.Common.FileBasic;

namespace GeneralUpdate.Core.Driver
{
    public abstract class DriverCommand
    {
        public abstract void Execute();

        /// <summary>
        /// Search for driver files.
        /// </summary>
        /// <param name="patchPath"></param>
        /// <returns></returns>
        protected static IEnumerable<FileInfo> SearchDrivers(string patchPath, string fileExtension)
        {
            var files = StorageManager.GetAllFiles(patchPath, StorageManager.SkipDirectorys);
            return files.Where(x => x.FullName.EndsWith(fileExtension)).ToList();
        }
    }
}