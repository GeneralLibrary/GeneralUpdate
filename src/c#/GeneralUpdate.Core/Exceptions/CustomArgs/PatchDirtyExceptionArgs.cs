using System;
using System.Collections.Generic;
using System.Text;

namespace GeneralUpdate.Core.Exceptions.CustomArgs
{
    [Serializable]
    public sealed class PatchDirtyExceptionArgs : ExceptionArgs
    {
        private readonly String _patchPath;

        public PatchDirtyExceptionArgs(String patchPath) { _patchPath = patchPath; }

        public String PatchPath { get { return _patchPath; } }

        public override string Message 
        {
            get 
            {
                return (_patchPath == null) ? base.Message : $"Patch file path {_patchPath}";
            }
        }
    }
}
