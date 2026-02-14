using System;
using System.Collections.Generic;
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

        #region Constructor Tests

        /// <summary>
        /// Tests that the constructor properly initializes with valid parameters.
        /// </summary>
        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Act
            var builder = new ConfiginfoBuilder(TestUpdateUrl, TestToken, TestScheme);

            // Assert
            Assert.NotNull(builder);
        }

        /// <summary>
        /// Tests that the Create factory method properly initializes with valid parameters.
        /// </summary>
        [Fact]
        public void Create_WithValidParameters_CreatesInstance()
        {
            // Act
            var builder = ConfiginfoBuilder.Create(TestUpdateUrl, TestToken, TestScheme);

            // Assert
            Assert.NotNull(builder);
        }

        /// <summary>
        /// Tests that Create factory method produces same result as constructor.
        /// </summary>
        [Fact]
        public void Create_ProducesSameResultAsConstructor()
        {
            // Act
            var config1 = new ConfiginfoBuilder(TestUpdateUrl, TestToken, TestScheme).Build();
            var config2 = ConfiginfoBuilder.Create(TestUpdateUrl, TestToken, TestScheme).Build();

            // Assert
            Assert.Equal(config1.UpdateUrl, config2.UpdateUrl);
            Assert.Equal(config1.Token, config2.Token);
            Assert.Equal(config1.Scheme, config2.Scheme);
            Assert.Equal(config1.InstallPath, config2.InstallPath);
            Assert.Equal(config1.AppName, config2.AppName);
        }

        /// <summary>
        /// Tests that the constructor throws ArgumentException when UpdateUrl is null.
        /// </summary>
        [Fact]
        public void Constructor_WithNullUpdateUrl_ThrowsArgumentException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                new ConfiginfoBuilder(null, TestToken, TestScheme));
            
            Assert.Contains("UpdateUrl", exception.Message);
        }

        /// <summary>
        /// Tests that the constructor throws ArgumentException when UpdateUrl is empty.
        /// </summary>
        [Fact]
        public void Constructor_WithEmptyUpdateUrl_ThrowsArgumentException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                new ConfiginfoBuilder("", TestToken, TestScheme));
            
            Assert.Contains("UpdateUrl", exception.Message);
        }

        /// <summary>
        /// Tests that the constructor throws ArgumentException when UpdateUrl is not a valid URI.
        /// </summary>
        [Fact]
        public void Constructor_WithInvalidUpdateUrl_ThrowsArgumentException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                new ConfiginfoBuilder("not-a-valid-url", TestToken, TestScheme));
            
            Assert.Contains("UpdateUrl", exception.Message);
            Assert.Contains("valid absolute URI", exception.Message);
        }

        /// <summary>
        /// Tests that the constructor throws ArgumentException when Token is null.
        /// </summary>
        [Fact]
        public void Constructor_WithNullToken_ThrowsArgumentException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                new ConfiginfoBuilder(TestUpdateUrl, null, TestScheme));
            
            Assert.Contains("Token", exception.Message);
        }

        /// <summary>
        /// Tests that the constructor throws ArgumentException when Scheme is null.
        /// </summary>
        [Fact]
        public void Constructor_WithNullScheme_ThrowsArgumentException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                new ConfiginfoBuilder(TestUpdateUrl, TestToken, null));
            
            Assert.Contains("Scheme", exception.Message);
        }

        #endregion

        #region Build Method Tests

        /// <summary>
        /// Tests that Build() creates a valid Configinfo object with default values.
        /// </summary>
        [Fact]
        public void Build_WithMinimalParameters_ReturnsValidConfiginfo()
        {
            // Arrange
            var builder = new ConfiginfoBuilder(TestUpdateUrl, TestToken, TestScheme);

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

        /// <summary>
        /// Tests that Build() creates Configinfo with platform-specific defaults.
        /// </summary>
        [Fact]
        public void Build_GeneratesPlatformSpecificDefaults()
        {
            // Arrange
            var builder = new ConfiginfoBuilder(TestUpdateUrl, TestToken, TestScheme);

            // Act
            var config = builder.Build();

            // Assert
            Assert.NotNull(config.InstallPath);
            
            // InstallPath should be the current application's base directory
            Assert.Equal(AppDomain.CurrentDomain.BaseDirectory, config.InstallPath);
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows-specific assertions
                Assert.Contains("App.exe", config.AppName);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Linux-specific assertions
                Assert.DoesNotContain(".exe", config.AppName);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // macOS-specific assertions
                Assert.DoesNotContain(".exe", config.AppName);
            }
        }

        /// <summary>
        /// Tests that Build() initializes collection properties with empty lists.
        /// </summary>
        [Fact]
        public void Build_InitializesCollectionProperties()
        {
            // Arrange
            var builder = new ConfiginfoBuilder(TestUpdateUrl, TestToken, TestScheme);

            // Act
            var config = builder.Build();

            // Assert
            Assert.NotNull(config.BlackFiles);
            Assert.NotNull(config.BlackFormats);
            Assert.NotNull(config.SkipDirectorys);
            Assert.Contains(".log", config.BlackFormats);
            Assert.Contains(".tmp", config.BlackFormats);
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
            var builder = new ConfiginfoBuilder(TestUpdateUrl, TestToken, TestScheme);
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
            var builder = new ConfiginfoBuilder(TestUpdateUrl, TestToken, TestScheme);

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
            var builder = new ConfiginfoBuilder(TestUpdateUrl, TestToken, TestScheme);

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
            var builder = new ConfiginfoBuilder(TestUpdateUrl, TestToken, TestScheme);
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
            var builder = new ConfiginfoBuilder(TestUpdateUrl, TestToken, TestScheme);
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
            var builder = new ConfiginfoBuilder(TestUpdateUrl, TestToken, TestScheme);
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
            var builder = new ConfiginfoBuilder(TestUpdateUrl, TestToken, TestScheme);
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
            var builder = new ConfiginfoBuilder(TestUpdateUrl, TestToken, TestScheme);
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
            var builder = new ConfiginfoBuilder(TestUpdateUrl, TestToken, TestScheme);
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
            var builder = new ConfiginfoBuilder(TestUpdateUrl, TestToken, TestScheme);
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
            var builder = new ConfiginfoBuilder(TestUpdateUrl, TestToken, TestScheme);

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
            var builder = new ConfiginfoBuilder(TestUpdateUrl, TestToken, TestScheme);
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
            var builder = new ConfiginfoBuilder(TestUpdateUrl, TestToken, TestScheme);
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
            var builder = new ConfiginfoBuilder(TestUpdateUrl, TestToken, TestScheme);
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
            var builder = new ConfiginfoBuilder(TestUpdateUrl, TestToken, TestScheme);
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
            var builder = new ConfiginfoBuilder(TestUpdateUrl, TestToken, TestScheme);
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
            var builder = new ConfiginfoBuilder(TestUpdateUrl, TestToken, TestScheme);
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
            var builder = new ConfiginfoBuilder(TestUpdateUrl, TestToken, TestScheme);
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
            // Arrange & Act
            var config = new ConfiginfoBuilder(TestUpdateUrl, TestToken, TestScheme)
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
            var builder = new ConfiginfoBuilder(TestUpdateUrl, TestToken, TestScheme);

            // Act
            var config = builder.Build();

            // Assert
            Assert.Contains("App.exe", config.AppName);
            // Should use the current application's base directory
            Assert.Equal(AppDomain.CurrentDomain.BaseDirectory, config.InstallPath);
            // Windows script should be empty
            Assert.Empty(config.Script);
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
            var builder = new ConfiginfoBuilder(TestUpdateUrl, TestToken, TestScheme);

            // Act
            var config = builder.Build();

            // Assert
            Assert.DoesNotContain(".exe", config.AppName);
            // Should use the current application's base directory
            Assert.Equal(AppDomain.CurrentDomain.BaseDirectory, config.InstallPath);
            // Linux should have a default permission script
            Assert.NotEmpty(config.Script);
            Assert.Contains("chmod", config.Script);
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
            var builder = new ConfiginfoBuilder(TestUpdateUrl, TestToken, TestScheme);

            // Act
            var config = builder.Build();

            // Assert
            Assert.DoesNotContain(".exe", config.AppName);
            // Should use the current application's base directory
            Assert.Equal(AppDomain.CurrentDomain.BaseDirectory, config.InstallPath);
            // macOS should have a default permission script
            Assert.NotEmpty(config.Script);
            Assert.Contains("chmod", config.Script);
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
            var builder = new ConfiginfoBuilder(TestUpdateUrl, TestToken, TestScheme);

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
            var builder = new ConfiginfoBuilder(TestUpdateUrl, TestToken, TestScheme);

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
        /// Tests that project metadata (version, company, etc.) is extracted from csproj when available.
        /// The builder should attempt to extract Version and Company/Authors fields.
        /// </summary>
        [Fact]
        public void Build_AttemptsToExtractProjectMetadata()
        {
            // Arrange
            var builder = new ConfiginfoBuilder(TestUpdateUrl, TestToken, TestScheme);

            // Act
            var config = builder.Build();

            // Assert - Core fields should always be set (either extracted or defaults)
            Assert.NotNull(config.ClientVersion);
            Assert.NotEmpty(config.ClientVersion);
            Assert.NotNull(config.ProductId);
            Assert.NotEmpty(config.ProductId);
            
            // Version should follow a reasonable format if extracted (e.g., "1.0.0" or similar)
            // If extracted from project file, it might have proper semantic versioning
            // If using default, it should still be a valid string
        }

        /// <summary>
        /// Tests a complete real-world scenario of building a Configinfo.
        /// </summary>
        [Fact]
        public void CompleteScenario_BuildsValidConfiginfo()
        {
            // Arrange
            var updateUrl = "https://api.example.com/updates";
            var token = "Bearer abc123xyz";
            var scheme = "https";

            // Act
            var config = new ConfiginfoBuilder(updateUrl, token, scheme)
                .SetAppName("MyApplication.exe")
                .SetMainAppName("MyApplication.exe")
                .SetClientVersion("1.5.2")
                .SetUpgradeClientVersion("1.0.0")
                .SetAppSecretKey("super-secret-key-456")
                .SetProductId("my-product-001")
                .SetInstallPath("/opt/myapp")
                .SetUpdateLogUrl("https://example.com/changelog")
                .SetReportUrl("https://api.example.com/report")
                .SetBlackFormats(new List<string> { ".log", ".tmp", ".cache" })
                .Build();

            // Assert
            Assert.NotNull(config);
            Assert.Equal(updateUrl, config.UpdateUrl);
            Assert.Equal(token, config.Token);
            Assert.Equal(scheme, config.Scheme);
            Assert.Equal("MyApplication.exe", config.AppName);
            Assert.Equal("1.5.2", config.ClientVersion);
            Assert.Equal("/opt/myapp", config.InstallPath);
            
            // Should pass validation
            config.Validate();
        }

        #endregion
    }
}
