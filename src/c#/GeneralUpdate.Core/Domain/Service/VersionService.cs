using GeneralUpdate.Core.Domain.DTO;
using GeneralUpdate.Core.Domain.Enum;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Domain.Service
{
    public class VersionService
    {
        public async Task<VersionRespDTO> ValidationVersion(string url, Action<object, ProgressType, string> statusCallback)
        {
            statusCallback(this, ProgressType.Check, "Update checking...");
            VersionRespDTO resp = await ValidationVersion(url);
            if (resp == null) statusCallback(this, ProgressType.Check, $"Request failed , Code :{resp.Code}, Message:{resp.Message} !");
            return await ValidationVersion(url);
        }

        public async Task<VersionRespDTO> ValidationVersion(string url)
        {
            var updateResp = await GetTaskAsync<VersionRespDTO>(url);
            if (updateResp == null || updateResp.Body == null) throw new ArgumentNullException($"The  verification request is abnormal, please check the network or parameter configuration!");
            if (updateResp.Code != HttpStatus.OK) throw new Exception($"Request failed , Code :{updateResp.Code}, Message:{updateResp.Message} !");
            if (updateResp.Code == HttpStatus.OK) return updateResp;
            return null;
        }

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

        private static bool CheckValidationResult(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) => true;
    }
}