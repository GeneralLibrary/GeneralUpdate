using GeneralUpdate.Infrastructure.DataServices.Http;

namespace GeneralUpdate.PacketTool.Services
{
    public class MainService
    {
        public async Task PostUpgradPakcet<T>(string filePath, int clientType, string version, string clientAppKey,Action<T> reponseCallback) where T : class 
        {
            await HttpService.Instance.PostFileRequest<T>(filePath, clientType, version, clientAppKey, "upload", reponseCallback);
        }
    }
}
