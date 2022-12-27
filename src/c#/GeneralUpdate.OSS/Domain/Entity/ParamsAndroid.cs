namespace GeneralUpdate.OSS.Domain.Entity
{
    public class ParamsAndroid : GeneralUpdate.Core.Domain.Entity.Entity
    {
        public string Url { get; set; }

        public string Apk { get; set; }

        public string CurrentVersion { get; set; }

        public string Authority { get; set; }

        public string VersionFileName { get; set; }

        public ParamsAndroid(string url, string apk, string currentVersion, string authority, string versionFileName)
        {
            Url = url ?? throw new ArgumentNullException(nameof(url));
            Apk = apk ?? throw new ArgumentNullException(nameof(apk));
            CurrentVersion = currentVersion ?? throw new ArgumentNullException(nameof(currentVersion));
            Authority = authority ?? throw new ArgumentNullException(nameof(authority));
            VersionFileName = versionFileName ?? "versions.json";
        }
    }
}
