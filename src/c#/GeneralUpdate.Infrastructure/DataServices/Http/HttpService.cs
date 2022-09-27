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

        public async Task PostFileRequest<T>(string url, Dictionary<string, string> parameters, string filePath , Action<T> reponseCallback, int timeOutInMillisecond = 1000 * 10) where T : class
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = new TimeSpan(timeOutInMillisecond);
                    var content = new MultipartFormDataContent();
                    content.Add(new MultipartFormDataContentProvider(parameters));
                    if (File.Exists(filePath))
                    {
                        var fileName = Path.GetFileName(filePath);
                        content.Add(new ByteArrayContent(File.ReadAllBytes(filePath)), "file", fileName);
                    }
                    var response = await client.PostAsync(url, content).Result.Content.ReadAsStringAsync();
                    var obj = JsonSerializer.Deserialize<T>(response, _serializerOptions);
                    reponseCallback(obj);
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
