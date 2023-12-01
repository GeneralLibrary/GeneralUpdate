using GeneralUpdate.SystemService.Services;

namespace GeneralUpdate.SystemService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddHostedService<WillMessageService>();
            var host = builder.Build();
            host.Run();
        }
    }
}