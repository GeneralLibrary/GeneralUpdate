using Newtonsoft.Json;

namespace GeneralUpdate.ProtectService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateSlimBuilder(args);
            var app = builder.Build();
            var protectApi = app.MapGroup("/protect");
            protectApi.MapGet("/{backupJson}/{targetDir}", Restore);
            app.Run();
        }

        internal static string Restore(string backupJson,string targetDirectory)
        {
            var backupObj =  JsonConvert.DeserializeObject<Stack<List<string>>>(backupJson);
            while (backupObj?.Count > 0)
            {
                List<string> currentList = backupObj.Pop();
                foreach (var filePath in currentList)
                {
                    string fileName = Path.GetFileName(filePath);
                    string destFile = Path.Combine(targetDirectory, fileName);
                    File.Copy(filePath, destFile, true); 
                }
            }
            return backupJson;
        }
    }
}
