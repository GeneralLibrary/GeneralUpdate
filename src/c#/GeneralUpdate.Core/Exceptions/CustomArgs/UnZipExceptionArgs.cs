using System;
using System.Collections.Generic;
using System.Text;

namespace GeneralUpdate.Core.Exceptions.CustomArgs
{
    [Serializable]
    internal class UnZipExceptionArgs : ExceptionArgs
    {
        private readonly String _filePath;

        public UnZipExceptionArgs(String filePath) { _filePath = filePath; }

        public String FilePath { get { return _filePath; } }

        public override string Message
        {
            get
            {
                return (_filePath == null) ? base.Message : $"Patch file path {_filePath}";
            }
        }
    }
}
