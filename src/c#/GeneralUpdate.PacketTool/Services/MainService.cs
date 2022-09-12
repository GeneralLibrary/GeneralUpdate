using GeneralUpdate.Infrastructure.DataServices.Http;

namespace GeneralUpdate.PacketTool.Services
{
    public class MainService
    {
        public async Task PostUpgradPakcet<T>(string filePath,Action<T> reponseCallback) where T : class 
        {
            await HttpService.Instance.PostFileRequest<T>(filePath, "", reponseCallback);
        }
    }
}
