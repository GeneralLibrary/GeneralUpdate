using System;
using System.Text.Json.Serialization;

namespace GeneralUpdate.Core.Configuration
{
    public class GlobalConfigInfoOSS
    {
        [JsonPropertyName("Url")]
        public string Url { get; set; }

        [JsonPropertyName("UpdateAppName")]
        public string UpgradeAppName { get; set; }

        [JsonPropertyName("CurrentVersion")]
        public string CurrentVersion { get; set; }

        [JsonPropertyName("VersionFileName")]
        public string VersionFileName { get; set; }
        
        [JsonPropertyName("Encoding")]
        public string Encoding { get; set; }

        public GlobalConfigInfoOSS()
        {
        }

        public GlobalConfigInfoOSS(string url, string appName, string currentVersion, string versionFileName)
        {
            Url = url ?? throw new ArgumentNullException(nameof(url));
            UpgradeAppName = appName ?? throw new ArgumentNullException(nameof(appName));
            CurrentVersion = currentVersion ?? throw new ArgumentNullException(nameof(currentVersion));
            VersionFileName = versionFileName ?? "versions.json";
        }
    }
}