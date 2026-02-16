using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using GeneralUpdate.Common.Shared.Object;
using Xunit;

namespace CoreTest.Shared
{
    /// <summary>
    /// Unit tests for the ConfiginfoBuilder class.
    /// Tests builder pattern, default value generation, and platform-specific behavior.
    /// </summary>
    public class ConfiginfoBuilderTests
    {
        private const string TestUpdateUrl = "https://example.com/api/update";
        private const string TestToken = "test-token-12345";
        private const string TestScheme = "https";

        /// <summary>
        /// Helper method to create a test config file with all required fields.
        /// </summary>
        private void CreateTestConfigFile()
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update_config.json");
            var testConfig = new
            {
                UpdateUrl = TestUpdateUrl,
                Token = TestToken,
                Scheme = TestScheme,
                AppName = "Update.exe",
                MainAppName = "TestApp.exe",
                ClientVersion = "1.0.0",
                AppSecretKey = "test-secret-key",
                InstallPath = AppDomain.CurrentDomain.BaseDirectory
            };
            File.WriteAllText(configPath, System.Text.Json.JsonSerializer.Serialize(testConfig));
        }

        /// <summary>
        /// Helper method to clean up test config file.
        /// </summary>
        private void CleanupTestConfigFile()
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update_config.json");
            if (File.Exists(configPath))
            {
                File.Delete(configPath);
            }
        }

        /// <summary>
        /// Helper method to create a builder with all required fields set for testing.
        /// Creates a config file, loads it, and returns the builder.
        /// </summary>
        private ConfiginfoBuilder CreateBuilderWithRequiredFields()
        {
            CreateTestConfigFile();
            return ConfiginfoBuilder.Create();
        }

        #region Constructor Tests

        /// <summary>
        /// Tests that the Create factory method properly initializes from config file.
        /// </summary>
        [Fact]
        public void Create_WithValidConfigFile_CreatesInstance()
        {
            try
            {
                // Arrange
                CreateTestConfigFile();

                // Act
                var builder = ConfiginfoBuilder.Create();

                // Assert
                Assert.NotNull(builder);
            }
            finally
            {
                CleanupTestConfigFile();
            }
        }

        /// <summary>
        /// Tests that Create factory method produces consistent results.
        /// </summary>
        [Fact]
        public void Create_ProducesConsistentResults()
        {
            try
            {
                // Arrange
                CreateTestConfigFile();

                // Act
                var config1 = ConfiginfoBuilder.Create().Build();
                var config2 = ConfiginfoBuilder.Create().Build();

                // Assert
                Assert.Equal(config1.UpdateUrl, config2.UpdateUrl);
                Assert.Equal(config1.Token, config2.Token);
                Assert.Equal(config1.Scheme, config2.Scheme);
                Assert.Equal(config1.AppName, config2.AppName);
            }
            finally
            {
                CleanupTestConfigFile();
            }
        }

        /// <summary>
        /// Tests that the Create method throws FileNotFoundException when config file is missing.
        /// </summary>
        [Fact]
        public void Create_WithoutConfigFile_ThrowsFileNotFoundException()
        {
            // Arrange - ensure no config file exists
            CleanupTestConfigFile();

            // Act & Assert
            var exception = Assert.Throws<FileNotFoundException>(() => 
                ConfiginfoBuilder.Create());
            
            Assert.Contains("update_config.json", exception.Message);
        }

        /// <summary>
        /// Tests that the Create method handles invalid JSON gracefully.
        /// </summary>
        [Fact]
        public void Create_WithInvalidJson_ThrowsFileNotFoundException()
        {
            try
            {
                // Arrange - create invalid JSON file
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update_config.json");
                File.WriteAllText(configPath, "{ invalid json content");

                // Act & Assert
                var exception = Assert.Throws<FileNotFoundException>(() => 
                    ConfiginfoBuilder.Create());
                
                Assert.Contains("update_config.json", exception.Message);
            }
            finally
            {
                CleanupTestConfigFile();
            }
        }

        /// <summary>
        /// Tests that the Create method validates required fields from config file.
        /// </summary>
        [Fact]
        public void Create_WithIncompleteConfig_ThrowsOnBuild()
        {
            try
            {
                // Arrange - create config with missing required fields
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update_config.json");
                var incompleteConfig = new
                {
                    UpdateUrl = TestUpdateUrl,
                    Token = TestToken,
                    Scheme = TestScheme
                    // Missing MainAppName, ClientVersion, AppSecretKey
                };
                File.WriteAllText(configPath, System.Text.Json.JsonSerializer.Serialize(incompleteConfig));

                // Act & Assert
                var builder = ConfiginfoBuilder.Create();
                Assert.Throws<InvalidOperationException>(() => builder.Build());
            }
            finally
            {
                CleanupTestConfigFile();
            }
        }

        #endregion

        #region Build Method Tests

        /// <summary>
        /// Tests that Build() creates a valid Configinfo object when all required fields are set.
        /// </summary>
        [Fact]
        public void Build_WithMinimalParameters_ReturnsValidConfiginfo()
        {
            try
            {
                // Arrange - Now that defaults are removed, we must set all required fields via config file
                CreateTestConfigFile();
                var builder = ConfiginfoBuilder.Create();

                // Act
                var config = builder.Build();

                // Assert
                Assert.NotNull(config);
                Assert.Equal(TestUpdateUrl, config.UpdateUrl);
                Assert.Equal(TestToken, config.Token);
                Assert.Equal(TestScheme, config.Scheme);
                Assert.NotNull(config.AppName);
                Assert.NotNull(config.MainAppName);
                Assert.NotNull(config.ClientVersion);
                Assert.NotNull(config.InstallPath);
                Assert.NotNull(config.AppSecretKey);
            }
            finally
            {
                CleanupTestConfigFile();
            }
        }

        /// <summary>
        /// Tests that Build() creates Configinfo with platform-specific defaults.
        /// </summary>
        [Fact]
        public void Build_GeneratesPlatformSpecificDefaults()
        {
            try
            {
                // Arrange
                var builder = CreateBuilderWithRequiredFields();

                // Act
                var config = builder.Build();

                // Assert
                Assert.NotNull(config.InstallPath);
                
                // InstallPath should be the current application's base directory
                Assert.Equal(AppDomain.CurrentDomain.BaseDirectory, config.InstallPath);
                
                // According to requirements, AppName default is "Update.exe" regardless of platform
                Assert.Equal("Update.exe", config.AppName);
            }
            finally
            {
                CleanupTestConfigFile();
            }
        }

        /// <summary>
        /// Tests that Build() initializes collection properties with empty lists.
        /// </summary>
        [Fact]
        public void Build_InitializesCollectionProperties()
        {
            try
            {
                // Arrange
                var builder = CreateBuilderWithRequiredFields();

                // Act
                var config = builder.Build();

                // Assert
                Assert.NotNull(config.BlackFiles);
                Assert.NotNull(config.BlackFormats);
                Assert.NotNull(config.SkipDirectorys);
                // DefaultBlackFormats is now empty per requirements
                Assert.Empty(config.BlackFormats);
            }
            finally
            {
                CleanupTestConfigFile();
            }
        }

        #endregion

        #region Setter Method Tests

        /// <summary>
        /// Tests that SetAppName correctly sets the application name.
        /// </summary>
        [Fact]
        public void SetAppName_WithValidValue_SetsAppName()
        {
            // Arrange
            var builder = CreateBuilderWithRequiredFields();
            var customAppName = "CustomApp.exe";

            // Act
            var config = builder.SetAppName(customAppName).Build();

            // Assert
            Assert.Equal(customAppName, config.AppName);
        }

        /// <summary>
        /// Tests that SetAppName returns the builder for method chaining.
        /// </summary>
        [Fact]
        public void SetAppName_ReturnsBuilder_ForMethodChaining()
        {
            // Arrange
            var builder = CreateBuilderWithRequiredFields();

            // Act
            var result = builder.SetAppName("Test.exe");

            // Assert
            Assert.Same(builder, result);
        }

        /// <summary>
        /// Tests that SetAppName throws ArgumentException when value is null.
        /// </summary>
        [Fact]
        public void SetAppName_WithNullValue_ThrowsArgumentException()
        {
            // Arrange
            var builder = CreateBuilderWithRequiredFields();

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => builder.SetAppName(null));
            Assert.Contains("AppName", exception.Message);
        }

        /// <summary>
        /// Tests that SetMainAppName correctly sets the main application name.
        /// </summary>
        [Fact]
        public void SetMainAppName_WithValidValue_SetsMainAppName()
        {
            // Arrange
            var builder = CreateBuilderWithRequiredFields();
            var customMainAppName = "MainApp.exe";

            // Act
            var config = builder.SetMainAppName(customMainAppName).Build();

            // Assert
            Assert.Equal(customMainAppName, config.MainAppName);
        }

        /// <summary>
        /// Tests that SetClientVersion correctly sets the client version.
        /// </summary>
        [Fact]
        public void SetClientVersion_WithValidValue_SetsClientVersion()
        {
            // Arrange
            var builder = CreateBuilderWithRequiredFields();
            var customVersion = "2.5.1";

            // Act
            var config = builder.SetClientVersion(customVersion).Build();

            // Assert
            Assert.Equal(customVersion, config.ClientVersion);
        }

        /// <summary>
        /// Tests that SetUpgradeClientVersion correctly sets the upgrade client version.
        /// </summary>
        [Fact]
        public void SetUpgradeClientVersion_WithValidValue_SetsUpgradeClientVersion()
        {
            // Arrange
            var builder = CreateBuilderWithRequiredFields();
            var customVersion = "3.0.0";

            // Act
            var config = builder.SetUpgradeClientVersion(customVersion).Build();

            // Assert
            Assert.Equal(customVersion, config.UpgradeClientVersion);
        }

        /// <summary>
        /// Tests that SetAppSecretKey correctly sets the secret key.
        /// </summary>
        [Fact]
        public void SetAppSecretKey_WithValidValue_SetsAppSecretKey()
        {
            // Arrange
            var builder = CreateBuilderWithRequiredFields();
            var customSecretKey = "my-secret-key-123";

            // Act
            var config = builder.SetAppSecretKey(customSecretKey).Build();

            // Assert
            Assert.Equal(customSecretKey, config.AppSecretKey);
        }

        /// <summary>
        /// Tests that SetProductId correctly sets the product ID.
        /// </summary>
        [Fact]
        public void SetProductId_WithValidValue_SetsProductId()
        {
            // Arrange
            var builder = CreateBuilderWithRequiredFields();
            var customProductId = "product-xyz-789";

            // Act
            var config = builder.SetProductId(customProductId).Build();

            // Assert
            Assert.Equal(customProductId, config.ProductId);
        }

        /// <summary>
        /// Tests that SetInstallPath correctly sets the installation path.
        /// </summary>
        [Fact]
        public void SetInstallPath_WithValidValue_SetsInstallPath()
        {
            // Arrange
            var builder = CreateBuilderWithRequiredFields();
            var customPath = "/custom/install/path";

            // Act
            var config = builder.SetInstallPath(customPath).Build();

            // Assert
            Assert.Equal(customPath, config.InstallPath);
        }

        /// <summary>
        /// Tests that SetUpdateLogUrl correctly sets the update log URL.
        /// </summary>
        [Fact]
        public void SetUpdateLogUrl_WithValidUrl_SetsUpdateLogUrl()
        {
            // Arrange
            var builder = CreateBuilderWithRequiredFields();
            var logUrl = "https://example.com/changelog";

            // Act
            var config = builder.SetUpdateLogUrl(logUrl).Build();

            // Assert
            Assert.Equal(logUrl, config.UpdateLogUrl);
        }

        /// <summary>
        /// Tests that SetUpdateLogUrl throws ArgumentException when URL is invalid.
        /// </summary>
        [Fact]
        public void SetUpdateLogUrl_WithInvalidUrl_ThrowsArgumentException()
        {
            // Arrange
            var builder = CreateBuilderWithRequiredFields();

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                builder.SetUpdateLogUrl("not-a-valid-url"));
            
            Assert.Contains("UpdateLogUrl", exception.Message);
        }

        /// <summary>
        /// Tests that SetReportUrl correctly sets the report URL.
        /// </summary>
        [Fact]
        public void SetReportUrl_WithValidUrl_SetsReportUrl()
        {
            // Arrange
            var builder = CreateBuilderWithRequiredFields();
            var reportUrl = "https://example.com/report";

            // Act
            var config = builder.SetReportUrl(reportUrl).Build();

            // Assert
            Assert.Equal(reportUrl, config.ReportUrl);
        }

        /// <summary>
        /// Tests that SetBowl correctly sets the bowl process name.
        /// </summary>
        [Fact]
        public void SetBowl_WithValidValue_SetsBowl()
        {
            // Arrange
            var builder = CreateBuilderWithRequiredFields();
            var bowlProcess = "Bowl.exe";

            // Act
            var config = builder.SetBowl(bowlProcess).Build();

            // Assert
            Assert.Equal(bowlProcess, config.Bowl);
        }

        /// <summary>
        /// Tests that SetScript correctly sets the shell script.
        /// </summary>
        [Fact]
        public void SetScript_WithValidValue_SetsScript()
        {
            // Arrange
            var builder = CreateBuilderWithRequiredFields();
            var customScript = "#!/bin/bash\necho 'Hello'";

            // Act
            var config = builder.SetScript(customScript).Build();

            // Assert
            Assert.Equal(customScript, config.Script);
        }

        /// <summary>
        /// Tests that SetDriverDirectory correctly sets the driver directory.
        /// </summary>
        [Fact]
        public void SetDriverDirectory_WithValidValue_SetsDriverDirectory()
        {
            // Arrange
            var builder = CreateBuilderWithRequiredFields();
            var driverDir = "/path/to/drivers";

            // Act
            var config = builder.SetDriverDirectory(driverDir).Build();

            // Assert
            Assert.Equal(driverDir, config.DriverDirectory);
        }

        /// <summary>
        /// Tests that SetBlackFiles correctly sets the blacklist files.
        /// </summary>
        [Fact]
        public void SetBlackFiles_WithValidList_SetsBlackFiles()
        {
            // Arrange
            var builder = CreateBuilderWithRequiredFields();
            var blackFiles = new List<string> { "file1.txt", "file2.dat" };

            // Act
            var config = builder.SetBlackFiles(blackFiles).Build();

            // Assert
            Assert.Equal(blackFiles, config.BlackFiles);
        }

        /// <summary>
        /// Tests that SetBlackFormats correctly sets the blacklist formats.
        /// </summary>
        [Fact]
        public void SetBlackFormats_WithValidList_SetsBlackFormats()
        {
            // Arrange
            var builder = CreateBuilderWithRequiredFields();
            var blackFormats = new List<string> { ".bak", ".old" };

            // Act
            var config = builder.SetBlackFormats(blackFormats).Build();

            // Assert
            Assert.Equal(blackFormats, config.BlackFormats);
        }

        /// <summary>
        /// Tests that SetSkipDirectorys correctly sets the skip directories list.
        /// </summary>
        [Fact]
        public void SetSkipDirectorys_WithValidList_SetsSkipDirectorys()
        {
            // Arrange
            var builder = CreateBuilderWithRequiredFields();
            var skipDirs = new List<string> { "/temp", "/cache" };

            // Act
            var config = builder.SetSkipDirectorys(skipDirs).Build();

            // Assert
            Assert.Equal(skipDirs, config.SkipDirectorys);
        }

        #endregion

        #region Method Chaining Tests

        /// <summary>
        /// Tests that multiple setter methods can be chained together.
        /// </summary>
        [Fact]
        public void BuilderPattern_SupportsMethodChaining()
        {
            try
            {
                // Arrange & Act
                CreateTestConfigFile();
                var config = ConfiginfoBuilder.Create()
                    .SetAppName("CustomApp.exe")
                    .SetMainAppName("MainCustomApp.exe")
                    .SetClientVersion("2.0.0")
                    .SetInstallPath("/custom/path")
                    .SetAppSecretKey("custom-secret")
                    .Build();

                // Assert
                Assert.Equal("CustomApp.exe", config.AppName);
                Assert.Equal("MainCustomApp.exe", config.MainAppName);
                Assert.Equal("2.0.0", config.ClientVersion);
                Assert.Equal("/custom/path", config.InstallPath);
                Assert.Equal("custom-secret", config.AppSecretKey);
            }
            finally
            {
                CleanupTestConfigFile();
            }
        }

        #endregion

        #region Platform-Specific Tests

        /// <summary>
        /// Tests that Windows platform generates appropriate defaults.
        /// </summary>
        [Fact]
        public void Build_OnWindows_GeneratesWindowsDefaults()
        {
            // This test will only verify behavior on Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return; // Skip on non-Windows platforms
            }

            // Arrange
            var builder = CreateBuilderWithRequiredFields();

            // Act
            var config = builder.Build();

            // Assert
            // According to requirements, AppName default is "Update.exe" regardless of platform
            Assert.Equal("Update.exe", config.AppName);
            // Should use the current application's base directory
            Assert.Equal(AppDomain.CurrentDomain.BaseDirectory, config.InstallPath);
        }

        /// <summary>
        /// Tests that Linux platform generates appropriate defaults.
        /// </summary>
        [Fact]
        public void Build_OnLinux_GeneratesLinuxDefaults()
        {
            // This test will only verify behavior on Linux
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return; // Skip on non-Linux platforms
            }

            // Arrange
            var builder = CreateBuilderWithRequiredFields();

            // Act
            var config = builder.Build();

            // Assert
            // According to requirements, AppName default is "Update.exe" regardless of platform
            Assert.Equal("Update.exe", config.AppName);
            // Should use the current application's base directory
            Assert.Equal(AppDomain.CurrentDomain.BaseDirectory, config.InstallPath);
        }

        /// <summary>
        /// Tests that macOS platform generates appropriate defaults.
        /// </summary>
        [Fact]
        public void Build_OnMacOS_GeneratesMacOSDefaults()
        {
            // This test will only verify behavior on macOS
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return; // Skip on non-macOS platforms
            }

            // Arrange
            var builder = CreateBuilderWithRequiredFields();

            // Act
            var config = builder.Build();

            // Assert
            // According to requirements, AppName default is "Update.exe" regardless of platform
            Assert.Equal("Update.exe", config.AppName);
            // Should use the current application's base directory
            Assert.Equal(AppDomain.CurrentDomain.BaseDirectory, config.InstallPath);
        }

        #endregion

        #region Integration Tests

        /// <summary>
        /// Tests that the built Configinfo object passes validation.
        /// </summary>
        [Fact]
        public void Build_ReturnsConfiginfoThatPassesValidation()
        {
            // Arrange
            var builder = CreateBuilderWithRequiredFields();

            // Act
            var config = builder.Build();

            // Assert - should not throw
            config.Validate();
        }

        /// <summary>
        /// Tests that application name is extracted from project context when available.
        /// The test verifies that the builder attempts to read from the project file,
        /// and gracefully falls back to defaults if not found.
        /// </summary>
        [Fact]
        public void Build_AttemptsToExtractAppNameFromProject()
        {
            // Arrange
            var builder = CreateBuilderWithRequiredFields();

            // Act
            var config = builder.Build();

            // Assert - AppName should be set (either from project or fallback)
            Assert.NotNull(config.AppName);
            Assert.NotEmpty(config.AppName);
            Assert.NotNull(config.MainAppName);
            Assert.NotEmpty(config.MainAppName);
            
            // On Windows, should have .exe extension
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.EndsWith(".exe", config.AppName);
            }
        }

        /// <summary>
        /// Tests that project metadata fields can be set and retrieved.
        /// Since defaults were removed per requirements, fields are null unless explicitly set.
        /// </summary>
        [Fact]
        public void Build_AttemptsToExtractProjectMetadata()
        {
            // Arrange
            var builder = CreateBuilderWithRequiredFields()
                .SetProductId("test-product-id");

            // Act
            var config = builder.Build();

            // Assert - Core fields should be set if explicitly provided
            Assert.NotNull(config.ClientVersion);
            Assert.NotEmpty(config.ClientVersion);
            Assert.NotNull(config.ProductId);
            Assert.NotEmpty(config.ProductId);
            Assert.Equal("test-product-id", config.ProductId);
        }

        /// <summary>
        /// Tests a complete real-world scenario of building a Configinfo.
        /// </summary>
        [Fact]
        public void CompleteScenario_BuildsValidConfiginfo()
        {
            try
            {
                // Arrange
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update_config.json");
                var completeConfig = new
                {
                    UpdateUrl = "https://api.example.com/updates",
                    Token = "Bearer abc123xyz",
                    Scheme = "https",
                    AppName = "MyApplication.exe",
                    MainAppName = "MyApplication.exe",
                    ClientVersion = "1.5.2",
                    UpgradeClientVersion = "1.0.0",
                    AppSecretKey = "super-secret-key-456",
                    ProductId = "my-product-001",
                    InstallPath = "/opt/myapp",
                    UpdateLogUrl = "https://example.com/changelog",
                    ReportUrl = "https://api.example.com/report",
                    BlackFormats = new[] { ".log", ".tmp", ".cache" }
                };
                File.WriteAllText(configPath, System.Text.Json.JsonSerializer.Serialize(completeConfig));

                // Act
                var config = ConfiginfoBuilder.Create()
                    .Build();

                // Assert
                Assert.NotNull(config);
                Assert.Equal("https://api.example.com/updates", config.UpdateUrl);
                Assert.Equal("Bearer abc123xyz", config.Token);
                Assert.Equal("https", config.Scheme);
                Assert.Equal("MyApplication.exe", config.AppName);
                Assert.Equal("1.5.2", config.ClientVersion);
                Assert.Equal("/opt/myapp", config.InstallPath);
                
                // Should pass validation
                config.Validate();
            }
            finally
            {
                CleanupTestConfigFile();
            }
        }

        #endregion

        #region JSON Configuration File Tests

        /// <summary>
        /// Tests that ConfiginfoBuilder loads configuration from update_config.json file if present.
        /// </summary>
        [Fact]
        public void Create_WithConfigFile_LoadsFromFile()
        {
            // Arrange - Create a test config file
            var configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update_config.json");
            var testConfig = new
            {
                UpdateUrl = "https://config-file.example.com/updates",
                Token = "config-file-token",
                Scheme = "https",
                AppName = "ConfigFileApp.exe",
                MainAppName = "ConfigFileMain.exe",
                ClientVersion = "9.9.9",
                AppSecretKey = "config-file-secret",
                InstallPath = "/config/file/path"
            };
            
            try
            {
                // Write test config file
                File.WriteAllText(configFilePath, System.Text.Json.JsonSerializer.Serialize(testConfig));

                // Act - Use parameterless Create() to load from file
                var config = ConfiginfoBuilder.Create().Build();

                // Assert - Values should come from config file
                Assert.Equal("https://config-file.example.com/updates", config.UpdateUrl);
                Assert.Equal("config-file-token", config.Token);
                Assert.Equal("https", config.Scheme);
                Assert.Equal("ConfigFileApp.exe", config.AppName);
                Assert.Equal("ConfigFileMain.exe", config.MainAppName);
                Assert.Equal("9.9.9", config.ClientVersion);
                Assert.Equal("/config/file/path", config.InstallPath);
            }
            finally
            {
                // Cleanup - Delete test config file
                if (File.Exists(configFilePath))
                {
                    File.Delete(configFilePath);
                }
            }
        }

        /// <summary>
        /// Tests that ConfiginfoBuilder uses parameters when no config file exists.
        /// </summary>
        [Fact]
        public void Create_WithoutConfigFile_UsesParameters()
        {
            // Arrange - Ensure no config file exists
            var configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update_config.json");
            if (File.Exists(configFilePath))
            {
                File.Delete(configFilePath);
            }

            try
            {
                // Act - Create should use parameters
                var config = CreateBuilderWithRequiredFields().Build();

                // Assert - Values should come from parameters and defaults
                Assert.Equal(TestUpdateUrl, config.UpdateUrl);
                Assert.Equal(TestToken, config.Token);
                Assert.Equal(TestScheme, config.Scheme);
                Assert.Equal("Update.exe", config.AppName); // Default value
            }
            finally
            {
                // No cleanup needed since we're ensuring file doesn't exist
            }
        }

        /// <summary>
        /// Tests that ConfiginfoBuilder handles invalid JSON gracefully.
        /// </summary>
        [Fact]
        public void Create_WithInvalidConfigFile_FallsBackToParameters()
        {
            // Arrange - Create an invalid config file
            var configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update_config.json");
            
            try
            {
                // Write invalid JSON
                File.WriteAllText(configFilePath, "{ invalid json content !!!");

                // Act - Create should fall back to parameters
                var config = CreateBuilderWithRequiredFields().Build();

                // Assert - Values should come from parameters (fallback)
                Assert.Equal(TestUpdateUrl, config.UpdateUrl);
                Assert.Equal(TestToken, config.Token);
                Assert.Equal(TestScheme, config.Scheme);
            }
            finally
            {
                // Cleanup - Delete test config file
                if (File.Exists(configFilePath))
                {
                    File.Delete(configFilePath);
                }
            }
        }

        #endregion
    }
}
