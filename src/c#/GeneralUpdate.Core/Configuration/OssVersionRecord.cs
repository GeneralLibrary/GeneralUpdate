using System;
using System.Text.Json.Serialization;

namespace GeneralUpdate.Core.Configuration
{
    /// <summary>
    /// Version data persistence.
    /// </summary>
    public class OssVersionRecord : VersionIdentity
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
        public override string Hash { get; set; }

        /// <summary>
        /// The version number.
        /// </summary>
        [JsonPropertyName("Version")]
        public override string Version { get; set; }

        /// <summary>
        /// Remote service url address.
        /// </summary>
        [JsonPropertyName("Url")]
        public override string Url { get; set; }
    }
}