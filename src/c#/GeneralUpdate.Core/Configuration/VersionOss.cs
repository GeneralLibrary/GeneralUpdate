using System;
using System.Text.Json.Serialization;

namespace GeneralUpdate.Core.Configuration
{
    /// <summary>
    /// Version data persistence.
    /// </summary>
    public class VersionOss
    {
        /// <summary>
        /// Update package release time.
        /// </summary>
        
        [JsonPropertyName("PubTime")]
        public DateTime PubTime { get; set; }

        /// <summary>
        /// Update package name.
        /// </summary>
        [JsonPropertyName("PacketName")]
        public string PacketName { get; set; }

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