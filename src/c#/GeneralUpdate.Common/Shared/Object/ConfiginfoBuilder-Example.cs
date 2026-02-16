using System;
using System.Collections.Generic;
using System.IO;
using GeneralUpdate.Common.Shared.Object;

namespace ConfiginfoBuilderExample
{
    /// <summary>
    /// Example demonstrating the ConfiginfoBuilder usage with JSON configuration
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== ConfiginfoBuilder Usage Examples ===\n");

            // Example 1: Load configuration from JSON file (recommended)
            Console.WriteLine("Example 1: Loading from update_config.json file");
            Console.WriteLine("This example requires an update_config.json file in the running directory.");
            Console.WriteLine("The configuration file has the highest priority and must contain all required settings.\n");
            
            try
            {
                // Create update_config.json for demonstration
                CreateExampleConfigFile();
                
                // Simply call Create() with no parameters - it loads from update_config.json
                var config = ConfiginfoBuilder.Create().Build();
                
                Console.WriteLine($"  UpdateUrl: {config.UpdateUrl}");
                Console.WriteLine($"  Token: {config.Token}");
                Console.WriteLine($"  Scheme: {config.Scheme}");
                Console.WriteLine($"  InstallPath: {config.InstallPath}");
                Console.WriteLine($"  AppName: {config.AppName}");
                Console.WriteLine($"  ClientVersion: {config.ClientVersion}");
                Console.WriteLine();
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine($"  Error: {ex.Message}");
                Console.WriteLine("  Please create update_config.json in the running directory.");
                Console.WriteLine();
            }
            finally
            {
                CleanupExampleConfigFile();
            }

            // Example 2: Customizing configuration after loading from file
            Console.WriteLine("Example 2: Loading from JSON and customizing with method chaining");
            try
            {
                CreateExampleConfigFile();
                
                var customConfig = ConfiginfoBuilder.Create()
                    .SetAppName("CustomApp.exe")
                    .SetInstallPath("/custom/path")
                    .Build();
                
                Console.WriteLine($"  AppName: {customConfig.AppName}");
                Console.WriteLine($"  InstallPath: {customConfig.InstallPath}");
                Console.WriteLine();
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine($"  Error: {ex.Message}");
                Console.WriteLine();
            }
            finally
            {
                CleanupExampleConfigFile();
            }

            // Example 3: Error handling when config file is missing
            Console.WriteLine("Example 3: Error Handling - Missing Configuration File");
            try
            {
                var config = ConfiginfoBuilder.Create().Build();
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine($"  Caught expected error: {ex.Message}");
                Console.WriteLine("  This is expected when update_config.json doesn't exist.");
            }
            Console.WriteLine();

            Console.WriteLine("\n=== All Examples Completed! ===");
            Console.WriteLine("\nNote: ConfiginfoBuilder now requires update_config.json file.");
            Console.WriteLine("See update_config.example.json for a complete example.");
        }

        private static void CreateExampleConfigFile()
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update_config.json");
            var exampleConfig = @"{
  ""UpdateUrl"": ""https://api.example.com/updates"",
  ""Token"": ""example-auth-token"",
  ""Scheme"": ""https"",
  ""AppName"": ""Update.exe"",
  ""MainAppName"": ""MyApplication.exe"",
  ""ClientVersion"": ""1.0.0"",
  ""UpgradeClientVersion"": ""1.0.0"",
  ""AppSecretKey"": ""example-secret-key"",
  ""ProductId"": ""example-product-id""
}";
            File.WriteAllText(configPath, exampleConfig);
        }

        private static void CleanupExampleConfigFile()
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update_config.json");
            if (File.Exists(configPath))
            {
                File.Delete(configPath);
            }
        }
    }
}
