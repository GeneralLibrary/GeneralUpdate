using GeneralUpdate.Core.Domain.PO;
using System;
using System.IO;
using GeneralUpdate.Core.Utils;
using System.Diagnostics;
using Microsoft.Diagnostics.NETCore.Client;
using System.Collections.Generic;

namespace GeneralUpdate.Core.Testament
{
    public sealed class GeneralTestamentProvider
    {
        private const string TESTAMENT = "testament.json";
        private string _testamentPath;
        private TestamentPO _testamentPO;
        private string _url;

        public GeneralTestamentProvider(string url, string path)
        {
            _url = url;
            _testamentPath = Path.Combine(path, TESTAMENT);
        }

        /// <summary>
        /// Before the update, the files that need to be updated are backed up by a complete directory structure.
        /// </summary>
        /// <param name="files"></param>
        public void Preconditioning(List<string> files)
        {
            
        }

        /// <summary>
        /// When the program is started, the contents of the last note will be analyzed to restore the backup or continue to update the last note.
        /// </summary>
        public void Demolish()
        {
            _testamentPO = FileUtil.ReadJsonFile<TestamentPO>(_testamentPath);
            File.Delete(_testamentPath);
        }

        /// <summary>
        /// Generate the contents of the last word, read the last word when the next program starts for backup restoration or re-update.
        /// </summary>
        /// <param name="testament"></param>
        public void Build(TestamentPO testament)
        {
            FileUtil.CreateJsonFile(_testamentPath, TESTAMENT, _testamentPO);
            Dump();
            //HttpUtil.GetTaskAsync("");
        }

        /// <summary>
        /// Export the dump file of the current application when an unknown exception occurs.
        /// </summary>
        public void Dump() {
            var currentProcess = Process.GetCurrentProcess();
            int pid = currentProcess.Id;
            try
            {
                DumpType dumpType = DumpType.Full;
                string dumpFilePath = @"./minidump.dmp";
                var client = new DiagnosticsClient(pid);
                client.WriteDump(dumpType, dumpFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Could not create dump: {ex.Message}");
            }
        }
    }
}
