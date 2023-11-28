using Quartz;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeneralUpdate.MiniBowl.Jbos
{
    internal class CheckClientJob : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            if (!IsChromeRunning())
            {
                Process.Start("chrome.exe");
            }

            await Task.CompletedTask;
        }

        private bool IsChromeRunning()
        {
            foreach (var process in Process.GetProcesses())
            {
                if (process.ProcessName.ToLower().Contains("chrome"))
                    return true;
            }
            return false;
        }
    }
}
