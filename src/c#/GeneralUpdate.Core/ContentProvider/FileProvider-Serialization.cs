using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;

namespace GeneralUpdate.Core.ContentProvider
{
    public partial class FileProvider
    {
        public static void CreateJson<T>(string targetPath, T obj)
        {
            var folderPath = Path.GetDirectoryName(targetPath) ??
                             throw new ArgumentException("invalid path", nameof(targetPath));
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            
            var jsonString = JsonConvert.SerializeObject(obj);
            File.WriteAllText(targetPath, jsonString);
        }

        public static T GetJson<T>(string path)
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<T>(json);
            }
            return default(T);
        }

        /// <summary>
        /// Convert object to base64 string.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string Serialize(object obj)
        {
            if (obj == null) return string.Empty;
            var json = JsonConvert.SerializeObject(obj);
            var bytes = Encoding.Default.GetBytes(json);
            var base64str = Convert.ToBase64String(bytes);
            return base64str;
        }

        /// <summary>
        /// Convert base64 object to string.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="str"></param>
        /// <returns></returns>
        public static T Deserialize<T>(string str)
        {
            var obj = default(T);
            if (string.IsNullOrEmpty(str)) return obj;
            byte[] bytes = Convert.FromBase64String(str);
            var json = Encoding.Default.GetString(bytes);
            var result = JsonConvert.DeserializeObject<T>(json);
            return result;
        }
    }
}