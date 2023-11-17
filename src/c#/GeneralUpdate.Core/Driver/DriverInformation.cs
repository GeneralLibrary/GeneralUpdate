using System;
using System.Collections.Generic;
using System.Linq;

namespace GeneralUpdate.Core.Driver
{
    /// <summary>
    /// Driver information object.
    /// </summary>
    public class DriverInformation
    {
        public string InstallDirectory { get; private set; }
        public string OutPutDirectory { get; private set; }

        private DriverInformation(){}

        public class Builder
        {
            private DriverInformation _information = new DriverInformation();
            
            public Builder SetInstallDirectory(string installDirectory)
            {
                _information.InstallDirectory = installDirectory;
                return this;
            }

            public Builder SetOutPutDirectory(string outPutDirectory)
            {
                _information.OutPutDirectory = outPutDirectory;
                return this;
            }

            public DriverInformation Build()
            {
                if (string.IsNullOrWhiteSpace(_information.InstallDirectory) || 
                    string.IsNullOrWhiteSpace(_information.OutPutDirectory))
                {
                    throw new InvalidOperationException("Cannot create DriverInformation, not all fields are set.");
                }

                return _information;
            }
        }
    }
}