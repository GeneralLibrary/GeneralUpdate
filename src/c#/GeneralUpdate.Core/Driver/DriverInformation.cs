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
        /// <summary>
        /// Directory for storing the driver to be installed (Update the driver file in the package).
        /// </summary>
        public string InstallDirectory { get; private set; }

        /// <summary>
        /// All driver backup directories.
        /// </summary>
        public string OutPutDirectory { get; private set; }

        /// <summary>
        /// A collection of driver files to be backed up.
        /// </summary>
        public List<string> DriverNames { get; private set; }

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

            /// <summary>
            /// Find the collection of driver names that need to be updated from the update package.
            /// </summary>
            /// <param name="driverNames"></param>
            /// <returns></returns>
            public Builder SetDriverNames(List<string> driverNames)
            {
                _information.DriverNames = driverNames;
                return this;
            }

            public DriverInformation Build()
            {
                if (string.IsNullOrWhiteSpace(_information.InstallDirectory) || 
                    string.IsNullOrWhiteSpace(_information.OutPutDirectory) || 
                    !_information.DriverNames.Any())
                {
                    throw new InvalidOperationException("Cannot create DriverInformation, not all fields are set.");
                }

                return _information;
            }
        }
    }
}