using GeneralUpdate.Core.Domain.Entity;
using GeneralUpdate.Core.Domain.Enum;
using GeneralUpdate.Core.Events;
using GeneralUpdate.Core.Events.CommonArgs;
using GeneralUpdate.Core.Events.MutiEventArgs;
using System;
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

        public BaseContext()
        { }

        public BaseContext(VersionInfo version, string zipfilePath, string targetPath, string sourcePath, string format, Encoding encoding)
        {
            Version = version;
            ZipfilePath = zipfilePath;
            TargetPath = targetPath;
            SourcePath = sourcePath;
            Format = format;
            Encoding = encoding;
        }

        public void OnProgressEventAction(object handle, ProgressType type, string message)
        => EventManager.Instance.Dispatch<Action<object, MutiDownloadProgressChangedEventArgs>>(handle, new MutiDownloadProgressChangedEventArgs(Version, message));

        public void OnExceptionEventAction(object handle, Exception exception)
        => EventManager.Instance.Dispatch<Action<object, ExceptionEventArgs>>(handle, new ExceptionEventArgs(exception));
    }
}