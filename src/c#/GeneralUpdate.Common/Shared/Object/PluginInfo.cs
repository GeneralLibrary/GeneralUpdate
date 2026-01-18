using System;
using System.Text.Json.Serialization;

namespace GeneralUpdate.Common.Shared.Object
{
    /// <summary>
    /// Plugin information including version, type, and compatibility range.
    /// </summary>
    public class PluginInfo
    {
        /// <summary>
        /// Unique identifier for the plugin.
        /// </summary>
        [JsonPropertyName("pluginId")]
        public string? PluginId { get; set; }

        /// <summary>
        /// Plugin name.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Current version of the plugin.
        /// </summary>
        [JsonPropertyName("version")]
        public string? Version { get; set; }

        /// <summary>
        /// Plugin type (JavaScript, Lua, Python, WASM, ExternalExecutable).
        /// </summary>
        [JsonPropertyName("pluginType")]
        public int? PluginType { get; set; }

        /// <summary>
        /// Minimum client version that supports this plugin.
        /// </summary>
        [JsonPropertyName("minClientVersion")]
        public string? MinClientVersion { get; set; }

        /// <summary>
        /// Maximum client version that supports this plugin.
        /// </summary>
        [JsonPropertyName("maxClientVersion")]
        public string? MaxClientVersion { get; set; }

        /// <summary>
        /// Download URL for the plugin.
        /// </summary>
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        /// <summary>
        /// Hash for integrity verification.
        /// </summary>
        [JsonPropertyName("hash")]
        public string? Hash { get; set; }

        /// <summary>
        /// Plugin release date.
        /// </summary>
        [JsonPropertyName("releaseDate")]
        public DateTime? ReleaseDate { get; set; }

        /// <summary>
        /// File size of the plugin package.
        /// </summary>
        [JsonPropertyName("size")]
        public long? Size { get; set; }

        /// <summary>
        /// Package format (e.g., ZIP).
        /// </summary>
        [JsonPropertyName("format")]
        public string? Format { get; set; }

        /// <summary>
        /// Description of the plugin.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Whether the plugin upgrade is mandatory.
        /// </summary>
        [JsonPropertyName("isMandatory")]
        public bool? IsMandatory { get; set; }
    }
}
