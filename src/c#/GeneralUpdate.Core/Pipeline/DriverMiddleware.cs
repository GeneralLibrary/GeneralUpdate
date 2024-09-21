using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GeneralUpdate.Common.Internal.Pipeline;
using GeneralUpdate.Core.Driver;

namespace GeneralUpdate.Core.Pipeline;

public class DriverMiddleware : IMiddleware
{
    public Task InvokeAsync(PipelineContext context)
    {
        return Task.Run(() =>
        {
            var drivers = context.Get<List<string>>("Drivers");
            var sourcesPath = context.Get<string>("Sources");
            var targetPath = context.Get<string>("Target");
            var version = context.Get<string>("Version");
        
            var information = new DriverInformation.Builder()
                .SetInstallDirectory(Path.Combine(sourcesPath, version))
                .SetOutPutDirectory(Path.Combine(targetPath, version))
                .SetDriverNames(drivers)
                .Build();

            var processor = new DriverProcessor();
            processor.AddCommand(new BackupDriverCommand(information));
            processor.AddCommand(new DeleteDriverCommand(information));
            processor.AddCommand(new InstallDriverCommand(information));
            processor.ProcessCommands();
        });
    }
}