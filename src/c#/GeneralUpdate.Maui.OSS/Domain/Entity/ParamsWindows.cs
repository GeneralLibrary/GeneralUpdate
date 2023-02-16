using System;

namespace GeneralUpdate.Maui.OSS.Domain.Entity
{
    public class ParamsWindows : GeneralUpdate.Core.Domain.Entity.Entity
    {
        public string Url { get; set; }

        public string AppName { get; set; }

        public string CurrentVersion { get; set; }

        public string VersionFileName { get; set; }

        public ParamsWindows(string url, string appName, string currentVersion, string versionFileName)
        {
            Url = url ?? throw new ArgumentNullException(nameof(url));
            AppName = appName ?? throw new ArgumentNullException(nameof(appName));
            CurrentVersion = currentVersion ?? throw new ArgumentNullException(nameof(currentVersion));
            VersionFileName = versionFileName ?? "versions.json";
        }
    }
}