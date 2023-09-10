using GeneralUpdate.Core.Domain.PO;
using System;
using System.IO;
using GeneralUpdate.Core.Utils;
using System.Diagnostics;
using Microsoft.Diagnostics.NETCore.Client;
using System.Collections.Generic;

namespace GeneralUpdate.Core.Testament
{
    public sealed class TestamentManager
    {
        private const string TESTAMENT = "testament.json";
        private string _testamentPath;
        private TestamentPO _testamentPO;
        private string _path;

        public TestamentManager()
        {
            _testamentPath = Path.Combine(_path, TESTAMENT);
        }

        public void Preconditioning(List<string> files)
        {
            
        }

        public void Demolish()
        {
            _testamentPO = FileUtil.ReadJsonFile<TestamentPO>(_testamentPath);
            File.Delete(_testamentPath);
        }

        public void Build(TestamentPO testament)
        {
            FileUtil.CreateJsonFile(_testamentPath, TESTAMENT, _testamentPO);
            Dump();
        }

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
