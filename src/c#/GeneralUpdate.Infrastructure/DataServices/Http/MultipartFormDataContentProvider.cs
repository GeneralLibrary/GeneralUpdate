using System.Net.Http.Headers;
using System.Text;

namespace GeneralUpdate.Infrastructure.DataServices.Http
{
    public class MultipartFormDataContentProvider
    {
        public static MultipartFormDataContent CreateContent(byte[] bytes, string fileName, IDictionary<string, string> addParams)
        {
            var strBoundary = DateTime.Now.Ticks.ToString("x");
            var resultContent = new MultipartFormDataContent(strBoundary);
            var fileByteContent = CreateByteArrayContent("file", fileName, "application/x-zip", bytes);
            resultContent.Add(fileByteContent);
            var paramsByteContent = CreateParamsByteArrayContent(addParams);
            paramsByteContent.ForEach(element =>
            {
                resultContent.Add(element);
            });
            return resultContent;
        }

        private static ByteArrayContent CreateByteArrayContent(string key, string fileName, string fileContent,
    byte[] fileBytes)
        {
            var fileByteArrayContent = new ByteArrayContent(fileBytes);
            fileByteArrayContent.Headers.ContentType = new MediaTypeHeaderValue(fileContent);
            fileByteArrayContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
            {
                Name = key,
                FileName = fileName
            };
            return fileByteArrayContent;
        }

        private static List<ByteArrayContent> CreateParamsByteArrayContent(IDictionary<string, string> dic)
        {
            var list = new List<ByteArrayContent>();
            if (dic == null || dic.Count == 0) return list;
            foreach (var (key, value) in dic)
            {
                var valueBytes = Encoding.UTF8.GetBytes(value);
                var byteArray = new ByteArrayContent(valueBytes);
                byteArray.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                {
                    Name = key
                };
                list.Add(byteArray);
            }
            return list;
        }
    }
}
