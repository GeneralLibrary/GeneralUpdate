#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GeneralUpdate.Common.Internal.Pipeline;
using GeneralUpdate.Common.Shared;
using GeneralUpdate.Drivelution;
using GeneralUpdate.Drivelution.Abstractions.Models;

namespace GeneralUpdate.Core.Pipeline;

/// <summary>
/// Middleware for handling driver updates using GeneralUpdate.Drivelution functionality.
/// </summary>
public class DrivelutionMiddleware : IMiddleware
{
    [RequiresUnreferencedCode("Driver update process includes signature validation that may require runtime reflection on some platforms")]
    [RequiresDynamicCode("Driver update process includes signature validation that may require runtime code generation on some platforms")]
    public async Task InvokeAsync(PipelineContext context)
    {
        try
        {
            var driverDirectory = context.Get<string>("DriverDirectory");
            
            if (string.IsNullOrWhiteSpace(driverDirectory))
            {
                GeneralTracer.Info("Driver directory not specified in context, skipping driver update.");
                return;
            }

            if (!Directory.Exists(driverDirectory))
            {
                GeneralTracer.Info($"Driver directory does not exist: {driverDirectory}, skipping driver update.");
                return;
            }

            GeneralTracer.Info($"Starting driver update from directory: {driverDirectory}");

            // Get drivers from the specified directory
            var drivers = await GeneralDrivelution.GetDriversFromDirectoryAsync(driverDirectory);

            if (drivers == null || !drivers.Any())
            {
                GeneralTracer.Info($"No drivers found in directory: {driverDirectory}");
                return;
            }

            GeneralTracer.Info($"Found {drivers.Count} driver(s) in directory.");

            // Update each driver
            var successCount = 0;
            var failureCount = 0;
            var results = new List<UpdateResult>();

            foreach (var driver in drivers)
            {
                try
                {
                    GeneralTracer.Info($"Updating driver: {driver.Name} (Version: {driver.Version})");
                    
                    var result = await GeneralDrivelution.QuickUpdateAsync(driver);
                    results.Add(result);

                    if (result.Success)
                    {
                        successCount++;
                        GeneralTracer.Info($"Driver {driver.Name} updated successfully. Status: {result.Status}");
                    }
                    else
                    {
                        failureCount++;
                        var errorMessage = result.Error != null 
                            ? $"{result.Error.Code}: {result.Error.Message}" 
                            : "Unknown error";
                        GeneralTracer.Warn($"Driver {driver.Name} update failed. Error: {errorMessage}");
                    }
                }
                catch (Exception ex)
                {
                    failureCount++;
                    GeneralTracer.Error($"Exception while updating driver {driver.Name}", ex);
                }
            }

            GeneralTracer.Info($"Driver update completed. Success: {successCount}, Failed: {failureCount}");

            // Store results in context for potential later use
            context.Add("DriverUpdateResults", results);
        }
        catch (Exception ex)
        {
            GeneralTracer.Error("Error in DrivelutionMiddleware", ex);
            throw;
        }
    }
}
#endif
