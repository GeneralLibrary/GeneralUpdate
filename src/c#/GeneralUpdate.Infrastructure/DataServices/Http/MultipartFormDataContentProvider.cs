using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace GeneralUpdate.Infrastructure.DataServices.Http
{
    public class MultipartFormDataContentProvider
    {
        private static ByteArrayContent CreateByteArrayContent(string key, string fileName, string fileContent,
    byte[] fileBytes)
        {
            var fileByteArrayContent = new ByteArrayContent(fileBytes);
            fileByteArrayContent.Headers.ContentType = new MediaTypeHeaderValue(fileContent);
            fileByteArrayContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
            {
                Name = key, //接口匹配name
                FileName = fileName //附件文件名
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

        public static MultipartFormDataContent CreateContent(byte[] bytes,string fileName, IDictionary<string, string> addParams)
        {
            var strBoundary = DateTime.Now.Ticks.ToString("x"); //分隔符
            var resultContent = new MultipartFormDataContent(strBoundary);
            //文件
            var fileByteContent = CreateByteArrayContent("file", fileName, "application/x-zip", bytes);
            resultContent.Add(fileByteContent);
            //其它附加参数
            var paramsByteContent = CreateParamsByteArrayContent(addParams);
            paramsByteContent.ForEach(el =>
            {
                resultContent.Add(el);
            });
            return resultContent;
        }
    }
}
