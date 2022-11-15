using System;
using System.Collections.Generic;
using System.Text;

namespace GeneralUpdate.Core.Exceptions.CustomArgs
{
    [Serializable]
    public sealed class HttpExceptionArgs : ExceptionArgs
    {
        private readonly String _url, _errorMessage;
        private readonly int _code;

        public HttpExceptionArgs(String url, int code, string errorMessage) 
        {
            _url = url;
            _code = code; 
            _errorMessage = errorMessage; 
        }

        public String Url { get { return _url; } }

        public String ErrorMessage { get { return _errorMessage; } }

        public int Code { get { return _code; } }

        public override string Message
        {
            get
            {
                return (_url == null) ? base.Message : $"Failed to request this address {_url} , status code { _code } , mssage : {_errorMessage}";
            }
        }
    }
}
