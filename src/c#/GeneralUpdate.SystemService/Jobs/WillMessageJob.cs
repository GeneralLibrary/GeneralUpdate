using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeneralUpdate.SystemService.Jobs
{
    internal class WillMessageJob : IJob
    {
        internal const string GROUP = "WillMessageGroup";
        internal const string TRIGGER = "WillMessageTrigger";
        private readonly ISchedulerFactory _schedulerFactory;
        private IJobExecutionContext _jobExecutionContext;

        public WillMessageJob(ISchedulerFactory schedulerFactory)
        {
            _schedulerFactory = schedulerFactory;
        }

        public Task Execute(IJobExecutionContext context)
        {
            _jobExecutionContext = context;
            return ScanWillMessage();
        }

        private async Task ScanWillMessage() 
        {
            // 暂停任务
            var scheduler = await _schedulerFactory.GetScheduler();
            await scheduler.PauseJob(_jobExecutionContext.JobDetail.Key);
        }
    }
}
