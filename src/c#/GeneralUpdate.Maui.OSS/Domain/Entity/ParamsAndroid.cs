﻿namespace GeneralUpdate.Maui.OSS.Domain.Entity
{
    public class ParamsAndroid : GeneralUpdate.Core.Domain.Entity.Entity
    {
        public string Url { get; set; }

        public string Apk { get; set; }

        public string CurrentVersion { get; set; }

        public string Authority { get; set; }

        public string VersionFileName { get; set; }

        public ParamsAndroid(string url, string apk, string authority, string currentVersion, string versionFileName)
        {
            Url = IsURL(url) ? url : throw new ArgumentNullException(nameof(url));
            Apk = apk ?? throw new ArgumentNullException(nameof(apk));
            CurrentVersion = IsVersion(currentVersion) ? currentVersion : throw new ArgumentNullException(nameof(currentVersion));
            Authority = authority ?? throw new ArgumentNullException(nameof(authority));
            VersionFileName = versionFileName ?? "versions.json";
        }
    }
}