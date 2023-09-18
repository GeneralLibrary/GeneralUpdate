using GeneralUpdate.Core.Domain.PO;
using System;
using System.IO;
using GeneralUpdate.Core.Utils;
using System.Diagnostics;
using Microsoft.Diagnostics.NETCore.Client;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Testament
{
    public sealed class GeneralTestamentProvider
    {
        private const string TESTAMENT_FILE = "testament.json";
        private const string DUMP_FILE = "generaldump.dmp";
        private string _testamentPath, _dumpPath;
        private string _url;

        public GeneralTestamentProvider(string url, string path)
        {
            _url = url;
            _testamentPath = Path.Combine(path, TESTAMENT_FILE);
            _dumpPath = Path.Combine(path, DUMP_FILE);
        }

        /// <summary>
        /// When the program is started, the contents of the last note will be analyzed to restore the backup or continue to update the last note.
        /// </summary>
        public void Demolish()
        {
            //TODO: If the backup file is rolled back to the current process, the restoration file will fail...
            var testamentPO = FileUtil.ReadJsonFile<TestamentPO>(_testamentPath);
            File.Delete(_testamentPath);
        }

        /// <summary>
        /// Generate the contents of the last word, read the last word when the next program starts for backup restoration or re-update.
        /// </summary>
        /// <param name="testament"></param>
        public void Build(VersionPO version,Exception exception)
        {
            if (version == null) return;

            Task.Run(async () => 
            {
                //Generate last words locally.
                var testament = new TestamentPO();
                testament.Exception = exception;
                testament.Version = version;
                FileUtil.CreateJsonFile(_testamentPath, TESTAMENT_FILE, testament);

                //dump files locally everywhere.
                CreateDump();

                //Report the current update failure to the server.
                await HttpUtil.GetTaskAsync<string>(_url);
            });
        }

        /// <summary>
        /// Export the dump file of the current application when an unknown exception occurs.
        /// </summary>
        private void CreateDump() {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var client = new DiagnosticsClient(currentProcess.Id);
                client.WriteDump(DumpType.Full, _dumpPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Could not create dump: {ex.Message}");
            }
        }
    }
}
