using System.Text.Json.Serialization;

namespace GeneralUpdate.Maui.OSS.Internal
{
    public class VersionInfo
    {
        /// <summary>
        /// Update package release time.
        /// </summary>
        [JsonPropertyName("PubTime")]
        public long PubTime { get; set; }

        /// <summary>
        /// Update package name.
        /// </summary>
        [JsonPropertyName("Name")]
        public string Name { get; set; }

        /// <summary>
        /// Compare and verify with the downloaded update package.
        /// </summary>
        [JsonPropertyName("Hash")]
        public string Hash { get; set; }

        /// <summary>
        /// The version number.
        /// </summary>
        [JsonPropertyName("Version")]
        public string Version { get; set; }

        /// <summary>
        /// Remote service url address.
        /// </summary>
        [JsonPropertyName("Url")]
        public string Url { get; set; }
    }
}
