using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace GeneralUpdate.Common.Download
{
    public sealed class DownloadManager<TVersion>
    {
        private ImmutableList<Task<TVersion>> _downloadTasks;
        
        //具有通知能力，通知下载进度、下载速度、下载文件大小、下载文件信息、单个文件下载状态、所有文件下载状态
        //异常下载中断可以继续尝试下载吗
        //设置一组Uri可以同时下载
        //多线程异步流同时下载多个文件的且具有断点续传功能
        //超时时间

        public DownloadManager()
        {
            _downloadTasks = ImmutableList.Create<Task<TVersion>>();
        }
        
        
        /*public async Task DownloadFilesAsync()
        {
            foreach (var file in _downloadTasks)
            {
                _downloadTasks.Add(DownloadFileWithResumeAsync(file.Url, file.FilePath));
            }

            await Task.WhenAll(downloadTasks);
        }*/

        private async Task DownloadFileWithResumeAsync(string url, string filePath)
        {
            long existingLength = 0;

            if (File.Exists(filePath))
            {
                existingLength = new FileInfo(filePath).Length;
            }

            using HttpClient client = new HttpClient();
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);

            if (existingLength > 0)
            {
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existingLength, null);
            }

            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using Stream contentStream = await response.Content.ReadAsStreamAsync();
            using FileStream fileStream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.None, 8192, true);

            await foreach (var buffer in ReadStreamAsync(contentStream))
            {
                await fileStream.WriteAsync(buffer, 0, buffer.Length);
            }
        }
        
        private async IAsyncEnumerable<byte[]> ReadStreamAsync(Stream stream)
        {
            byte[] buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                byte[] actualBytes = new byte[bytesRead];
                Array.Copy(buffer, actualBytes, bytesRead);
                yield return actualBytes;
            }
        }
    }
}