

using Newtonsoft.Json;

namespace GeneralUpdate.SystemService.services
{
    internal class RestoreService : BackgroundService
    {
        private readonly ILogger<RestoreService> _logger;
        private const string UPGRADE_STATUS = "upgrade_status";
        private const string FAIL = "fail";
        private const string RESTORE = "restore";

        public RestoreService(ILogger<RestoreService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Run(async () => 
            {
                while (stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(1000 * 10);
                    var status = Environment.GetEnvironmentVariable(UPGRADE_STATUS, EnvironmentVariableTarget.Machine);
                    if (status.Equals(FAIL)) 
                    {
                        Restore();
                    }
                }
            });
        }

        private void Restore() 
        {
            var restoreJson = Environment.GetEnvironmentVariable(RESTORE, EnvironmentVariableTarget.Machine);
            if (!string.IsNullOrWhiteSpace(restoreJson))
            {
                var restoreObj = JsonConvert.DeserializeObject<Stack<List<string>>>(restoreJson);
                //TODO: Restore
            }
        }
    }
}
