using GeneralUpdate.Core.Domain.PO;
using System;
using System.IO;
using GeneralUpdate.Core.Utils;
using System.Diagnostics;
using Microsoft.Diagnostics.NETCore.Client;
using System.Threading.Tasks;
using System.Collections.Generic;

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
        /// Generate the contents of the last word, read the last word when the next program starts for backup restoration or re-update.
        /// </summary>
        /// <param name="testament"></param>
        public void Build(TestamentPO testament)
        {
            if (testament == null) return;

            Task.Run(async () => 
            {
                FileUtil.CreateJsonFile(_testamentPath, TESTAMENT_FILE, testament);
                CreateDump();
                var parameters = new Dictionary<string, string>
                {
                    { "TrackID", testament.TrackID },
                    { "Exception", testament.Exception.ToString() }
                };
                await HttpUtil.PostFileTaskAsync<string>(_url, parameters, _dumpPath);
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
