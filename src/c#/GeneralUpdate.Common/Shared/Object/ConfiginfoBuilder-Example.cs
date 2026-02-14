using System;
using System.Collections.Generic;
using GeneralUpdate.Common.Shared.Object;

namespace ConfiginfoBuilderExample
{
    /// <summary>
    /// Example demonstrating the ConfiginfoBuilder usage
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== ConfiginfoBuilder Usage Examples ===\n");

            // Example 1: Minimal configuration (recommended for most cases)
            Console.WriteLine("Example 1: Minimal Configuration - Using Factory Method");
            var minimalConfig = ConfiginfoBuilder
                .Create("https://api.example.com/updates", "your-auth-token", "https")
                .Build();
            
            Console.WriteLine($"  UpdateUrl: {minimalConfig.UpdateUrl}");
            Console.WriteLine($"  Token: {minimalConfig.Token}");
            Console.WriteLine($"  Scheme: {minimalConfig.Scheme}");
            Console.WriteLine($"  InstallPath: {minimalConfig.InstallPath}");
            Console.WriteLine($"  AppName: {minimalConfig.AppName}");
            Console.WriteLine($"  Default Black Formats: {string.Join(", ", ConfiginfoBuilder.DefaultBlackFormats)}");
            Console.WriteLine();

            // Example 2: Custom configuration with method chaining
            Console.WriteLine("Example 2: Custom Configuration - Using Constructor");
            var customConfig = new ConfiginfoBuilder(
                "https://api.example.com/updates",
                "Bearer abc123xyz",
                "https"
            )
            .SetAppName("MyApplication.exe")
            .SetMainAppName("MyApplication.exe")
            .SetClientVersion("2.1.0")
            .SetInstallPath("/opt/myapp")
            .SetAppSecretKey("super-secret-key-789")
            .Build();
            
            Console.WriteLine($"  AppName: {customConfig.AppName}");
            Console.WriteLine($"  ClientVersion: {customConfig.ClientVersion}");
            Console.WriteLine($"  InstallPath: {customConfig.InstallPath}");
            Console.WriteLine($"  AppSecretKey: {customConfig.AppSecretKey}");
            Console.WriteLine();

            // Example 3: Configuration with file filters
            Console.WriteLine("Example 3: With File Filters");
            var filteredConfig = new ConfiginfoBuilder(
                "https://api.example.com/updates",
                "token123",
                "https"
            )
            .SetBlackFiles(new List<string> { "config.json", "user.dat" })
            .SetBlackFormats(new List<string> { ".log", ".tmp", ".cache", ".bak" })
            .SetSkipDirectorys(new List<string> { "/temp", "/logs" })
            .Build();
            
            Console.WriteLine($"  Black Files: {string.Join(", ", filteredConfig.BlackFiles)}");
            Console.WriteLine($"  Black Formats: {string.Join(", ", filteredConfig.BlackFormats)}");
            Console.WriteLine($"  Skip Directories: {string.Join(", ", filteredConfig.SkipDirectorys)}");
            Console.WriteLine();

            // Example 4: Complete configuration
            Console.WriteLine("Example 4: Complete Configuration");
            var completeConfig = new ConfiginfoBuilder(
                updateUrl: "https://api.example.com/updates",
                token: "Bearer xyz789",
                scheme: "https"
            )
            .SetAppName("MyApp.exe")
            .SetMainAppName("MyApp.exe")
            .SetClientVersion("3.0.0")
            .SetUpgradeClientVersion("1.5.0")
            .SetProductId("myapp-001")
            .SetAppSecretKey("secret-key-456")
            .SetInstallPath("/opt/myapp")
            .SetUpdateLogUrl("https://myapp.example.com/changelog")
            .SetReportUrl("https://api.example.com/report")
            .SetBowl("Bowl.exe")
            .SetDriverDirectory("/opt/myapp/drivers")
            .Build();
            
            Console.WriteLine($"  ProductId: {completeConfig.ProductId}");
            Console.WriteLine($"  UpdateLogUrl: {completeConfig.UpdateLogUrl}");
            Console.WriteLine($"  ReportUrl: {completeConfig.ReportUrl}");
            Console.WriteLine($"  Bowl: {completeConfig.Bowl}");
            Console.WriteLine();

            // Example 5: Error handling
            Console.WriteLine("Example 5: Error Handling");
            try
            {
                var invalidConfig = new ConfiginfoBuilder(
                    null, // Invalid: null URL
                    "token",
                    "https"
                );
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"  Caught expected error: {ex.Message}");
            }

            try
            {
                var invalidConfig2 = new ConfiginfoBuilder(
                    "not-a-url", // Invalid: malformed URL
                    "token",
                    "https"
                );
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"  Caught expected error: {ex.Message}");
            }
            Console.WriteLine();

            // Example 6: Validate configuration
            Console.WriteLine("Example 6: Configuration Validation");
            var validConfig = new ConfiginfoBuilder(
                "https://api.example.com/updates",
                "token",
                "https"
            ).Build();
            
            try
            {
                validConfig.Validate();
                Console.WriteLine("  Configuration is valid!");
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"  Validation failed: {ex.Message}");
            }

            Console.WriteLine("\n=== All Examples Completed Successfully! ===");
        }
    }
}
