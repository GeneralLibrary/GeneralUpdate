using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace GeneralUpdate.Infrastructure.DataServices.Http
{
    public class HttpService
    {
        private static HttpService _instance;
        private static object _lock = new object();
        private string _url;
        private JsonSerializerOptions _serializerOptions;

        private HttpService()
        {
            _url = "";
        }

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

        public async Task PostFileRequest<T>(string filePath,string apiRoute, Action<T> reponseCallback) where T : class
        {
            T result = default;
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var remoteUri = $"{_url}{apiRoute}";
                    var content = new MultipartFormDataContent();
                    var fileName = Path.GetFileName(filePath);
                    content.Add(new ByteArrayContent(System.IO.File.ReadAllBytes(filePath)), "file", fileName);
                    var response = await client.PostAsync(remoteUri, content).Result.Content.ReadAsStringAsync();
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
