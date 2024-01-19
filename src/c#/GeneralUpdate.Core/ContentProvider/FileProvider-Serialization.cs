using Newtonsoft.Json;
using System.IO;

namespace GeneralUpdate.Core.ContentProvider
{
    public partial class FileProvider
    {
        public static void CreateJson<T>(string targetPath, T obj)
        {
            if (!Directory.Exists(targetPath)) Directory.CreateDirectory(targetPath);
            if (File.Exists(targetPath)) File.Delete(targetPath);
            var jsonString = JsonConvert.SerializeObject(obj);
            File.WriteAllText(targetPath, jsonString);
        }

        public static T GetJson<T>(string path)
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                if (json != null)
                {
                    return JsonConvert.DeserializeObject<T>(json);
                }
            }
            return default(T);
        }
    }
}