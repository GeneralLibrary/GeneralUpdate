namespace GeneralUpdate.Maui.OSS.Strategys
{
    /// <summary>
    /// OSS updates abstract Strategy.
    /// </summary>
    public abstract class AbstractStrategy : IStrategy
    {
        /// <summary>
        /// download file.
        /// </summary>
        /// <param name="url">remote service address</param>
        /// <param name="filePath">download file path.</param>
        /// <param name="action">progress report.</param>
        /// <returns></returns>
        public async Task DownloadFileAsync(string url, string filePath, Action<long, long> action)
        {
            try
            {
                var request = new HttpRequestMessage(new HttpMethod("GET"), url);
                var client = new HttpClient();
                var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
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
            catch (Exception ex)
            {
                 throw;
            }
        }

        /// <summary>
        /// 下载文件
        /// </summary>
        /// <param name="serverFileName"></param>
        /// <param name="localFileName"></param>
        /// <returns></returns>
        //public static async Task<bool> DownloadFileAsync(string uri, string localFileName, Action<long, long> action)
        //{
        //    var serverUrl = new Uri(uri);
        //    var directoryPath = Path.GetDirectoryName(localFileName);
        //    if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);
        //    var httpClient = new HttpClient();
        //    var responseMessage = await httpClient.GetAsync(serverUrl);
        //    if (responseMessage.IsSuccessStatusCode)
        //    {
        //        Task processTask = null;
        //        using (var fileStream = File.Create(localFileName))
        //        {
        //            using (var streamFromService = await responseMessage.Content.ReadAsStreamAsync())
        //            {
        //                Task copyTask = streamFromService.CopyToAsync(fileStream);
        //                processTask = new Task(() =>
        //                {
        //                    while (!copyTask.IsCompleted)
        //                    {
        //                        action?.Invoke(fileStream.Position, fileStream.Length);
        //                    }
        //                });
        //                processTask.Start();
        //                Task.WaitAll(new Task[] { copyTask, processTask });
        //            }
        //        }
        //        return true;
        //    }
        //    else 
        //    {
        //        return false;
        //    }
        //}

        /// <summary>
        /// Example Initialize the creation strategy.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="parameter"></param>
        public abstract void Create<T>(T parameter) where T : class;

        /// <summary>
        /// Execute the injected strategy.
        /// </summary>
        /// <returns></returns>
        public abstract Task Execute();
    }
}