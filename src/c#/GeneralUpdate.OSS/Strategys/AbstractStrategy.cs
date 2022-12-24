using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeneralUpdate.OSS.Strategys
{
    public abstract class AbstractStrategy : IStrategy
    {
        private readonly HttpClient _client;

        public async Task DownloadFileAsync(string url,string apk, Action<long, long> action)
        {
            var req = new HttpRequestMessage(new HttpMethod("GET"), url);
            var response = _client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead).Result;
            var allLength = response.Content.Headers.ContentLength;
            var stream = await response.Content.ReadAsStreamAsync();
            var file = $"{FileSystem.AppDataDirectory}/{apk}";
            await using var fileStream = new FileStream(file, FileMode.Create);
            await using (stream)
            {
                var buffer = new byte[10240];
                var readLength = 0;
                int length;
                while ((length = await stream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                {
                    readLength += length;
                    action(readLength, allLength!.Value);
                    fileStream.Write(buffer, 0, length);
                }
            }
        }

        public abstract void Create();

        public abstract void Excute();
    }
}
