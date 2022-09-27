using GeneralUpdate.Infrastructure.DataServices.Http;

namespace GeneralUpdate.PacketTool.Services
{
    public class MainService
    {
        public async Task PostUpgradPakcet<T>(string filePath, int clientType, string version, string clientAppKey,string md5,Action<T> reponseCallback) where T : class 
        {
            var remoteUrl = "http://127.0.0.1:5001/upload";
            var parameters = new Dictionary<string, string>();
            parameters.Add("clientType", clientType.ToString());
            parameters.Add("version", version);
            parameters.Add("clientAppKey", clientAppKey);
            parameters.Add("md5", md5);
            await HttpService.Instance.PostFileRequest(remoteUrl,parameters, filePath, reponseCallback);
        }
    }
}
