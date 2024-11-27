using System;
using System.Collections.Generic;

namespace GeneralUpdate.Core.Driver
{
    /// <summary>
    /// Driver information object.
    /// </summary>
    public class DriverInformation
    {
        public Dictionary<string, string> FieldMappings { get; private set; }
        
        public string DriverFileExtension { get; private set; }
        
        /// <summary>
        /// All driver backup directories.
        /// </summary>
        public string OutPutDirectory { get; private set; }
        
        public string DriverDirectory { get; private set; }

        /// <summary>
        /// A collection of driver files to be backed up.
        /// </summary>
        public IEnumerable<DriverInfo> Drivers { get; set; }

        private DriverInformation()
        { }
        
        public class Builder
        {
            private DriverInformation _information = new ();

            public Builder SetDriverFileExtension(string fileExtension)
            {
                _information.DriverFileExtension = fileExtension;
                return this;
            }
            
            public Builder SetOutPutDirectory(string outPutDirectory)
            {
                _information.OutPutDirectory = outPutDirectory;
                return this;
            }
            
            public Builder SetDriverDirectory(string driverDirectory)
            {
                _information.DriverDirectory = driverDirectory;
                return this;
            }
            
            public Builder SetFieldMappings(Dictionary<string, string> fieldMappings)
            {
                _information.FieldMappings = fieldMappings;
                return this;
            }

            public DriverInformation Build()
            {
                if (string.IsNullOrWhiteSpace(_information.OutPutDirectory) ||
                    string.IsNullOrWhiteSpace(_information.DriverFileExtension))
                {
                    throw new ArgumentNullException("Cannot create DriverInformation, not all fields are set.");
                }

                return _information;
            }
        }
    }
}