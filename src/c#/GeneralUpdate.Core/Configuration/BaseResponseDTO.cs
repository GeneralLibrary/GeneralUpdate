using System.Text.Json.Serialization;

namespace GeneralUpdate.Core.Configuration
{
    public class BaseResponseDTO<TBody>
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("body")]
        public TBody Body { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }
    }
}