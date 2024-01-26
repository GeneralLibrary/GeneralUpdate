using GeneralUpdate.Core.Driver;
using GeneralUpdate.Core.Pipelines.Context;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace GeneralUpdate.Core.Pipelines.Middleware
{
    /// <summary>
    /// Drive file processing class.
    /// </summary>
    public class DriveMiddleware : IMiddleware
    {
        public async Task InvokeAsync(BaseContext context, MiddlewareStack stack)
        {
            var drivers = GetAllDriverDirectories(context.TargetPath);
            var information = new DriverInformation.Builder()
                .SetInstallDirectory(Path.Combine(context.SourcePath, context.Version.ToString()))
                .SetOutPutDirectory(Path.Combine(context.TargetPath, context.Version.ToString()))
                .SetDriverNames(drivers)
                .Build();

            var processor = new DriverProcessor();
            processor.AddCommand(new BackupDriverCommand(information));
            processor.AddCommand(new DeleteDriverCommand(information));
            processor.AddCommand(new InstallDriverCommand(information));
            processor.ProcessCommands();

            var node = stack.Pop();
            if (node != null) await node.Next.Invoke(context, stack);
        }

        /// <summary>
        /// Identifies all folders containing driver files in the specified directory and returns the directory collection.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private List<string> GetAllDriverDirectories(string path)
        {
            var driverDirectories = new HashSet<string>();
            try
            {
                foreach (string filePath in Directory.GetFiles(path))
                {
                    if (IsDriverFile(filePath))
                        driverDirectories.Add(filePath);
                }

                foreach (string directory in Directory.GetDirectories(path))
                {
                    driverDirectories.UnionWith(GetAllDriverDirectories(directory));
                }
            }
            catch (UnauthorizedAccessException)
            {
                Trace.WriteLine("No access directory：" + path);
            }
            catch (PathTooLongException)
            {
                Trace.WriteLine("Path overlength：" + path);
            }

            return new List<string>(driverDirectories);
        }

        /// <summary>
        /// Match the driver installation boot file.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private bool IsDriverFile(string filePath) =>
            string.Equals(Path.GetExtension(filePath), ".inf", StringComparison.OrdinalIgnoreCase);
    }
}