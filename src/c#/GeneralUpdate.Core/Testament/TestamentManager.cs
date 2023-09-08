using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace GeneralUpdate.Core.Testament
{
    public sealed class TestamentManager
    {
        private const string PYTHON_INSATLL = "install.py";
        private const string TESTAMENT = "testament.json";
        private string _testamentPath,_pythonPath;

        public TestamentManager(string path) 
        {
            if(string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException("path");
            _testamentPath = Path.Combine(path, TESTAMENT);
            _pythonPath = Path.Combine(path, PYTHON_INSATLL);
        }

        public void Read() 
        {

        }

        public TestamentManager Build() 
        {
            return this;
        }

        public void Launch() 
        {
        }
    }
}
