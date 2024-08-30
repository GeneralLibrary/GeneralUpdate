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
        private List<TVersion> _versions;
        private ImmutableList<Task<TVersion>> _downloadTasks;
        
        public DownloadManager(List<TVersion> versions)
        {
            _versions = versions;
            _downloadTasks = ImmutableList.Create<Task<TVersion>>();
        }
        
        /*public async Task DownloadFilesAsync()
        {
            foreach (var version in _versions)
            {
                _downloadTasks.Add(DownloadFileWithResumeAsync(file.Url, file.FilePath));
            }

            await Task.WhenAll(_downloadTasks);
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