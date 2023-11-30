using GeneralUpdate.SystemService.Jobs;
using Quartz;
using Quartz.Impl;

namespace GeneralUpdate.SystemService.Services
{
    internal class QuartzService : BackgroundService
    {
        private readonly ILogger<QuartzService> _logger;
        private IJobDetail _job;
        private ITrigger _trigger;
        private IScheduler _scheduler;

        public QuartzService(ILogger<QuartzService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _scheduler = await new StdSchedulerFactory().GetScheduler();
            await _scheduler.Start();

            var runTime = DateBuilder.EvenMinuteDate(DateTimeOffset.UtcNow);

            _job = JobBuilder.Create<WillMessageJob>()
                .WithIdentity(nameof(WillMessageJob), WillMessageJob.GROUP)
                .Build();
            _trigger = TriggerBuilder.Create()
                .WithIdentity(WillMessageJob.TRIGGER, WillMessageJob.GROUP)
                .StartAt(runTime)
                .WithSimpleSchedule(x => x
                    .WithIntervalInMinutes(2)
                    .RepeatForever())
                .Build();

            await _scheduler.ScheduleJob(_job, _trigger);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);
            await _scheduler?.Shutdown();
        }
    }
}
