using GeneralUpdate.Core.Domain.DTO;
using GeneralUpdate.Core.Domain.Enum;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using GeneralUpdate.Core.Events;
using GeneralUpdate.Core.Events.CommonArgs;

namespace GeneralUpdate.Core.Domain.Service
{
    public class VersionService
    {
        public async Task<VersionRespDTO> ValidationVersion(string url)
        {
            var updateResp = await GetTaskAsync<VersionRespDTO>(url);
            if (updateResp == null || updateResp.Body == null)
            {
                throw new ArgumentNullException(
                    nameof(updateResp),
                    "The verification request is abnormal, please check the network or parameter configuration!"
                );
            }

            if (updateResp.Code == HttpStatus.OK)
            {
                return updateResp;
            }
            else
            {
                throw new WebException(
                    $"Request failed , Code :{updateResp.Code}, Message:{updateResp.Message} !"
                );
            }
        }

        private async Task<T> GetTaskAsync<T>(
            string httpUrl,
            string headerKey = null,
            string headerValue = null
        )
        {
            HttpWebResponse response = null;
            try
            {
                ServicePointManager.ServerCertificateValidationCallback = CheckValidationResult;
                var request = (HttpWebRequest)WebRequest.Create(httpUrl);
                request.Method = "GET";
                request.Accept = "text/html, application/xhtml+xml, */*";
                request.ContentType = "application/x-www-form-urlencoded";
                request.Timeout = 15000;
                if (!string.IsNullOrEmpty(headerKey) && !string.IsNullOrEmpty(headerValue))
                {
                    request.Headers[headerKey] = headerValue;
                }
                response = (HttpWebResponse)await request.GetResponseAsync();
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new WebException(
                        $"Response status code does not indicate success: {response.StatusCode}!"
                    );
                }
                var responseStream = response.GetResponseStream();
                if (responseStream == null)
                {
                    throw new WebException(
                        "Response stream is null, please check the network or parameter configuration!"
                    );
                }
                using (var reader = new StreamReader(responseStream, Encoding.UTF8))
                {
                    var tempStr = await reader.ReadToEndAsync();
                    var respContent = JsonConvert.DeserializeObject<T>(tempStr);
                    return respContent;
                }
            }
            catch (Exception ex)
            {
                EventManager.Instance.Dispatch<Action<object, ExceptionEventArgs>>(
                    this,
                    new ExceptionEventArgs(ex)
                );
                return default;
            }
            finally
            {
                response?.Close();
            }
        }

        private static bool CheckValidationResult(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors
        ) => true;
    }
}