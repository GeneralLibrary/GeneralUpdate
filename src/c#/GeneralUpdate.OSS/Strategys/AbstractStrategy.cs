namespace GeneralUpdate.OSS.OSSStrategys
{
    public abstract class AbstractStrategy : IStrategy
    {
        private readonly HttpClient _client;

        /// <summary>
        /// download file.
        /// </summary>
        /// <param name="url">remote service address</param>
        /// <param name="filePath">download file path.</param>
        /// <param name="action">progress report.</param>
        /// <returns></returns>
        public async Task DownloadFileAsync(string url, string filePath, Action<long, long> action)
        {
            var request = new HttpRequestMessage(new HttpMethod("GET"), url);
            var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            var totalLength = response.Content.Headers.ContentLength;
            var stream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(filePath, FileMode.Create);
            await using (stream)
            {
                var buffer = new byte[10240];
                var readLength = 0;
                int length;
                while ((length = await stream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                {
                    readLength += length;
                    if (action != null) action(readLength, totalLength!.Value);
                    fileStream.Write(buffer, 0, length);
                }
            }
        }

        public abstract void Create<T>(T parameter) where T : class;

        public abstract Task Excute();
    }
}