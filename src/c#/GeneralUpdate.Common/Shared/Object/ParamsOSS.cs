using System;
using System.Text.Json.Serialization;

namespace GeneralUpdate.Common.Shared.Object
{
    public class ParamsOSS
    {
        [JsonPropertyName("Url")]
        public string Url { get; set; }

        [JsonPropertyName("AppName")]
        public string AppName { get; set; }

        [JsonPropertyName("CurrentVersion")]
        public string CurrentVersion { get; set; }

        [JsonPropertyName("VersionFileName")]
        public string VersionFileName { get; set; }

        public ParamsOSS()
        {
        }

        public ParamsOSS(string url, string appName, string currentVersion, string versionFileName)
        {
            Url = url ?? throw new ArgumentNullException(nameof(url));
            AppName = appName ?? throw new ArgumentNullException(nameof(appName));
            CurrentVersion = currentVersion ?? throw new ArgumentNullException(nameof(currentVersion));
            VersionFileName = versionFileName ?? "versions.json";
        }
    }
}