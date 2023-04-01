using GeneralUpdate.Core.Domain.Entity;
using System;
using System.Collections.Generic;
using System.Text;

namespace GeneralUpdate.Core.Pipelines.Context
{
    /// <summary>
    /// Pipeline common content.
    /// </summary>
    public class BaseContext
    {
        public VersionInfo Version { get; set; }

        public string Name { get; set; }

        public string ZipfilePath { get; set; }

        public string TargetPath { get; set; }

        public string SourcePath { get; set; }

        public string Format { get; set; }

        public Encoding Encoding { get; set; }

        public List<string> BlackFiles { get; set; }

        public List<string> BlackFileFormats { get; set; }

        public BaseContext(VersionInfo version, string zipfilePath, string targetPath, string sourcePath, string format, Encoding encoding, List<string> files, List<string> fileFormats)
        {
            Version = version ?? throw new ArgumentNullException($"{nameof(VersionInfo)} Cannot be empty");
            ZipfilePath = string.IsNullOrWhiteSpace(zipfilePath) ? throw new ArgumentNullException($"{nameof(zipfilePath)} Cannot be empty") : zipfilePath;
            TargetPath = string.IsNullOrWhiteSpace(targetPath) ? throw new ArgumentNullException($"{nameof(targetPath)} Cannot be empty") : targetPath; ;
            SourcePath = string.IsNullOrWhiteSpace(sourcePath) ? throw new ArgumentNullException($"{nameof(sourcePath)} Cannot be empty") : sourcePath; ;
            Format = string.IsNullOrWhiteSpace(format) ? throw new ArgumentNullException($"{nameof(format)} Cannot be empty") : format;
            Encoding = encoding;
            BlackFiles = files;
            BlackFileFormats = fileFormats;
        }
    }
}