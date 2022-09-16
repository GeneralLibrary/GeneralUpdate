using Microsoft.Extensions.Configuration;

namespace GeneralUpdate.Infrastructure.Config
{
    public interface IConfigService
    {
        void Init(MauiAppBuilder appBuilder, IConfiguration configuration,string config = "GeneralUpdate.PacketTool.launchSettings.json");

        string GetSettings(string key);

        T GetObjectSettings<T>(string key) where T : class;
    }
}
