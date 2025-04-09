using System;
using System.Text.Json.Serialization;

namespace GeneralUpdate.Common.Shared.Object
{
    public class GlobalConfigInfoOSS
    {
        [JsonPropertyName("Url")]
        public string Url { get; set; }

        [JsonPropertyName("AppName")]
        public string AppName { get; set; }

        [JsonPropertyName("CurrentVersion")]
        public string CurrentVersion { get; set; }

        [JsonPropertyName("VersionFileName")]
        public string VersionFileName { get; set; }
        
        [JsonPropertyName("Encoding")]
        public string Encoding { get; set; }

        [JsonPropertyName("Extend")]
        public string Extend { get; set; }
        
        [JsonPropertyName("Extend2")]
        public string Extend2 { get; set; }
        
        public GlobalConfigInfoOSS()
        {
        }

        public GlobalConfigInfoOSS(string url, string appName, string currentVersion, string versionFileName)
        {
            Url = url ?? throw new ArgumentNullException(nameof(url));
            AppName = appName ?? throw new ArgumentNullException(nameof(appName));
            CurrentVersion = currentVersion ?? throw new ArgumentNullException(nameof(currentVersion));
            VersionFileName = versionFileName ?? "versions.json";
        }
    }
}