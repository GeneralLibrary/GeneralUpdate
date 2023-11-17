using System.IO;
using System.Threading.Tasks;
using GeneralUpdate.Core.Driver;
using GeneralUpdate.Core.Pipelines.Context;

namespace GeneralUpdate.Core.Pipelines.Middleware
{
    /// <summary>
    /// Drive file processing class.
    /// </summary>
    public class DriveMiddleware : IMiddleware
    {
        public async Task InvokeAsync(BaseContext context, MiddlewareStack stack)
        {
            var information = new DriverInformation.Builder()
                .SetInstallDirectory(Path.Combine(context.SourcePath,context.Version.ToString()))
                .SetOutPutDirectory(Path.Combine(context.TargetPath,context.Version.ToString()))
                .SetDriverNames(null)
                .Build();

            var processor = new DriverProcessor();
            // Backup driver.
            processor.AddCommand(new BackupDriverCommand(information));
            // Install the new driver, and if the installation fails, the backup is automatically restored.
            processor.AddCommand(new InstallDriverCommand(information));
            processor.ProcessCommands();
            var node = stack.Pop();
            if (node != null) await node.Next.Invoke(context, stack);
        }
    }
}