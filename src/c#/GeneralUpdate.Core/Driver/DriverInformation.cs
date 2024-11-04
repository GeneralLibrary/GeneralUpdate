using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeneralUpdate.Common;

namespace GeneralUpdate.Core.Driver
{
    /// <summary>
    /// Driver information object.
    /// </summary>
    public class DriverInformation
    {
        public string DriverFileExtension { get; private set; }
        
        /// <summary>
        /// All driver backup directories.
        /// </summary>
        public string OutPutDirectory { get; private set; }

        /// <summary>
        /// A collection of driver files to be backed up.
        /// </summary>
        public IEnumerable<FileInfo> Drivers { get; private set; }

        private DriverInformation()
        { }
        
        public class Builder
        {
            private DriverInformation _information = new DriverInformation();

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

            /// <summary>
            /// Find the collection of driver names that need to be updated from the update package.
            /// </summary>
            /// <param name="driverNames"></param>
            /// <returns></returns>
            public Builder SetDrivers(string driversPath, string fileExtension)
            {
                if(string.IsNullOrWhiteSpace(driversPath) || string.IsNullOrWhiteSpace(fileExtension)) 
                    return this;
                
                _information.Drivers = SearchDrivers(driversPath, fileExtension);
                return this;
            }

            public DriverInformation Build()
            {
                if (string.IsNullOrWhiteSpace(_information.OutPutDirectory) ||
                    string.IsNullOrWhiteSpace(_information.DriverFileExtension) ||
                    !_information.Drivers.Any())
                {
                    throw new ArgumentNullException("Cannot create DriverInformation, not all fields are set.");
                }

                return _information;
            }
            
            /// <summary>
            /// Search for driver files.
            /// </summary>
            /// <param name="patchPath"></param>
            /// <returns></returns>
            private IEnumerable<FileInfo> SearchDrivers(string patchPath, string fileExtension)
            {
                var files = GeneralFileManager.GetAllfiles(patchPath);
                return files.Where(x => x.FullName.EndsWith(fileExtension)).ToList();
            }
        }
    }
}