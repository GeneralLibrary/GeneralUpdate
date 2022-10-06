using System.Diagnostics;
using System.Text.Json;

namespace GeneralUpdate.Infrastructure.DataServices.Http
{
    public class HttpService
    {
        private static HttpService _instance;
        private static object _lock = new object();
        private JsonSerializerOptions _serializerOptions;

        public static HttpService Instance 
        {
            get 
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null) _instance = new HttpService();
                    }
                }
                return _instance; 
            } 
        }

        public async Task PostFileRequest<T>(string url, Dictionary<string, string> parameters, string filePath , Action<T> reponseCallback) where T : class
        {
            try
            {
                using (var fileStream = File.Open(filePath, FileMode.Open))
                {
                    var bytes = new byte[fileStream.Length];
                    fileStream.Read(bytes, 0, bytes.Length);
                    using (var client = new HttpClient())
                    {
                        var message = new HttpRequestMessage(HttpMethod.Post, url);
                        message.Content = MultipartFormDataContentProvider.CreateContent(bytes,Path.GetFileName(filePath), parameters);
                        var responseMessage = await client.SendAsync(message);
                        if (responseMessage.IsSuccessStatusCode)
                        {
                            var response = await responseMessage.Content.ReadAsStringAsync();
                            var obj = JsonSerializer.Deserialize<T>(response, _serializerOptions);
                            reponseCallback(obj);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(@"\tERROR {0}", ex.Message);
                reponseCallback(null);
            }
        }
    }
}
