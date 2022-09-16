using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace GeneralUpdate.Infrastructure.Config
{
    public class ConfigService : IConfigService
    {
        private IConfiguration _configuration;

        public void Init(MauiAppBuilder appBuilder, IConfiguration configuration,string config)
        {
            _configuration = configuration;
            Assembly assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(config);
            var configurationBuilder = new ConfigurationBuilder().AddJsonStream(stream).Build();
            appBuilder.Configuration.AddConfiguration(configurationBuilder);
        }

        public string GetSettings(string key) 
        {
            if(_configuration == null || string.IsNullOrWhiteSpace(key)) return null;
            return _configuration.GetRequiredSection(key).Get<string>();
        }

        public T GetObjectSettings<T>(string key) where T : class 
        {
            if (_configuration == null || string.IsNullOrWhiteSpace(key)) return null;
            return _configuration.GetRequiredSection(key).Get<T>();
        }
    }
}
