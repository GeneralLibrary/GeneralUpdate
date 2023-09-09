using GeneralUpdate.Core.Domain.PO;
using System;
using System.IO;
using GeneralUpdate.Core.Utils;

namespace GeneralUpdate.Core.Testament
{
    public sealed class TestamentManager
    {
        private const string PYTHON_INSATLL = "install.py";
        private const string TESTAMENT = "testament.json";
        private string _testamentPath, _pythonPath;
        private TestamentPO _testamentPO;
        private string path;

        public TestamentManager()
        {
            _testamentPath = Path.Combine(path, TESTAMENT);
            _pythonPath = Path.Combine(path, PYTHON_INSATLL);
        }

        public void Demolish()
        {
            _testamentPO = FileUtil.ReadJsonFile<TestamentPO>(_testamentPath);
            File.Delete(_testamentPath);
        }

        public void Build()
        {
        }
    }
}
