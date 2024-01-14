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
        public VersionInfo Version { get; private set; }
        public string Name { get; private set; }
        public string ZipfilePath { get; private set; }
        public string TargetPath { get; private set; }
        public string SourcePath { get; private set; }
        public string Format { get; private set; }
        public int AppType { get; private set; }
        public Encoding Encoding { get; private set; }
        public List<string> BlackFiles { get; private set; }
        public List<string> BlackFileFormats { get; private set; }

        private BaseContext() { }

        public class Builder
        {
            private readonly BaseContext _context = new BaseContext();

            public Builder SetVersion(VersionInfo version)
            {
                _context.Version = version ?? throw new ArgumentNullException($"{nameof(VersionInfo)} Cannot be empty");
                return this;
            }

            public Builder SetZipfilePath(string zipfilePath)
            {
                _context.ZipfilePath = string.IsNullOrWhiteSpace(zipfilePath) ? throw new ArgumentNullException($"{nameof(zipfilePath)} Cannot be empty") : zipfilePath;
                return this;
            }

            public Builder SetTargetPath(string targetPath)
            {
                _context.TargetPath = string.IsNullOrWhiteSpace(targetPath) ? throw new ArgumentNullException($"{nameof(targetPath)} Cannot be empty") : targetPath;
                return this;
            }

            public Builder SetSourcePath(string sourcePath)
            {
                _context.SourcePath = string.IsNullOrWhiteSpace(sourcePath) ? throw new ArgumentNullException($"{nameof(sourcePath)} Cannot be empty") : sourcePath;
                return this;
            }

            public Builder SetFormat(string format)
            {
                _context.Format = string.IsNullOrWhiteSpace(format) ? throw new ArgumentNullException($"{nameof(format)} Cannot be empty") : format;
                return this;
            }

            public Builder SetEncoding(Encoding encoding)
            {
                _context.Encoding = encoding;
                return this;
            }

            public Builder SetBlackFiles(List<string> files)
            {
                _context.BlackFiles = files;
                return this;
            }

            public Builder SetBlackFileFormats(List<string> fileFormats)
            {
                _context.BlackFileFormats = fileFormats;
                return this;
            }

            public Builder SetAppType(int type)
            {
                _context.AppType = type;
                return this;
            }

            public BaseContext Build() => _context;
        }
    }
}