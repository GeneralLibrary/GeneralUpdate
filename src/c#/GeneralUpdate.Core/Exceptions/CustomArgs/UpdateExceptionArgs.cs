using GeneralUpdate.Core.Domain.Entity;
using System;

namespace GeneralUpdate.Core.Exceptions.CustomArgs
{
    [Serializable]
    internal class UpdateExceptionArgs : ExceptionArgs
    {
        private readonly VersionInfo _versionInfo;
        private readonly String _excptionMessage;

        public UpdateExceptionArgs(VersionInfo info, String excptionMessage)
        {
            _versionInfo = info;
            _excptionMessage = excptionMessage;
        }

        public VersionInfo VersionInfo
        { get { return _versionInfo; } }

        public String ExcptionMessage
        { get { return _excptionMessage; } }

        public override string Message
        {
            get
            {
                return (_versionInfo == null) ? base.Message : $"An exception occurred updating the file {_versionInfo.Name} ,The version number is {_versionInfo.Version}. error message : {_excptionMessage} !";
            }
        }
    }
}