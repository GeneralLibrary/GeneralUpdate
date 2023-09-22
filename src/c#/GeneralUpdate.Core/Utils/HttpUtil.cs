using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Utils
{
    public sealed class HttpUtil
    {
        public static async Task<T> GetTaskAsync<T>(string http_url, string header_key = null, string header_value = null)
        {
            HttpWebResponse response = null;
            try
            {
                ServicePointManager.ServerCertificateValidationCallback = new System.Net.Security.RemoteCertificateValidationCallback(CheckValidationResult);
                string httpUri = http_url;
                var encoding = Encoding.GetEncoding("utf-8");
                var request = (HttpWebRequest)WebRequest.Create(httpUri);
                request.Method = "GET";
                request.Accept = "text/html, application/xhtml+xml, */*";
                request.ContentType = "application/x-www-form-urlencoded";
                request.Timeout = 15000;
                if (!string.IsNullOrEmpty(header_key) && !string.IsNullOrEmpty(header_value))
                {
                    request.Headers[header_key] = header_value;
                }
                response = (HttpWebResponse)await request.GetResponseAsync();
                if (response.StatusCode != HttpStatusCode.OK) return default(T);
                using (var reader = new StreamReader(response.GetResponseStream(), encoding))
                {
                    var tempStr = reader.ReadToEnd();
                    var respContent = JsonConvert.DeserializeObject<T>(tempStr);
                    return respContent;
                }
            }
            catch
            {
                return default(T);
            }
            finally
            {
                if (response != null) response.Close();
            }
        }

        public static async Task<T> PostFileTaskAsync<T>(string httpUrl, Dictionary<string,string> parameters, string filePath) 
        {
            try
            {
                Uri uri = new Uri(httpUrl);
                using (var client = new HttpClient())
                using (var content = new MultipartFormDataContent())
                {
                    foreach (var parameter in parameters) 
                    {
                        var stringContent = new StringContent(parameter.Value);
                        content.Add(stringContent, parameter.Key);
                    }

                    if (string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
                    {
                        var fileStream = File.OpenRead(filePath);
                        var fileInfo = new FileInfo(filePath);
                        var fileContent = new StreamContent(fileStream);
                        content.Add(fileContent, "file", Path.GetFileName(filePath));
                    }

                    var result = await client.PostAsync(uri, content);
                    var reseponseJson = await result.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<T>(reseponseJson);
                }
            }
            catch
            {
                return default(T);
            }
        }

        private static bool CheckValidationResult(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) => true;
    }
}