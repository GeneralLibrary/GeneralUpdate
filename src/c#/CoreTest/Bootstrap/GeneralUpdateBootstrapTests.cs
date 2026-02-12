using GeneralUpdate.Common.Download;
using GeneralUpdate.Common.Internal;
using GeneralUpdate.Common.Shared.Object;
using GeneralUpdate.Core;
using Xunit;

namespace CoreTest.Bootstrap
{
    /// <summary>
    /// Contains test cases for the GeneralUpdateBootstrap class.
    /// Tests the main orchestrator for platform-agnostic update process.
    /// </summary>
    public class GeneralUpdateBootstrapTests
    {
        /// <summary>
        /// Tests that GeneralUpdateBootstrap can be instantiated.
        /// </summary>
        [Fact]
        public void Constructor_CreatesInstance()
        {
            // Act
            var bootstrap = new GeneralUpdateBootstrap();

            // Assert
            Assert.NotNull(bootstrap);
        }

        /// <summary>
        /// Tests that SetConfig returns the bootstrap instance for chaining.
        /// </summary>
        [Fact]
        public void SetConfig_ReturnsBootstrapInstance()
        {
            // Arrange
            var bootstrap = new GeneralUpdateBootstrap();
            var configInfo = new Configinfo
            {
                InstallPath = "/test/install",
                MainAppName = "TestApp.exe",
                UpdateUrl = "https://example.com/update",
                ClientVersion = "1.0.0"
            };

            // Act
            var result = bootstrap.SetConfig(configInfo);

            // Assert
            Assert.Same(bootstrap, result);
        }

        /// <summary>
        /// Tests that SetCustomSkipOption returns the bootstrap instance for chaining.
        /// </summary>
        [Fact]
        public void SetCustomSkipOption_ReturnsBootstrapInstance()
        {
            // Arrange
            var bootstrap = new GeneralUpdateBootstrap();
            Func<bool> skipFunc = () => false;

            // Act
            var result = bootstrap.SetCustomSkipOption(skipFunc);

            // Assert
            Assert.Same(bootstrap, result);
        }

        /// <summary>
        /// Tests that AddListenerMultiAllDownloadCompleted returns the bootstrap instance for chaining.
        /// </summary>
        [Fact]
        public void AddListenerMultiAllDownloadCompleted_ReturnsBootstrapInstance()
        {
            // Arrange
            var bootstrap = new GeneralUpdateBootstrap();
            Action<object, MultiAllDownloadCompletedEventArgs> callback = (sender, e) => { };

            // Act
            var result = bootstrap.AddListenerMultiAllDownloadCompleted(callback);

            // Assert
            Assert.Same(bootstrap, result);
        }

        /// <summary>
        /// Tests that AddListenerMultiDownloadCompleted returns the bootstrap instance for chaining.
        /// </summary>
        [Fact]
        public void AddListenerMultiDownloadCompleted_ReturnsBootstrapInstance()
        {
            // Arrange
            var bootstrap = new GeneralUpdateBootstrap();
            Action<object, MultiDownloadCompletedEventArgs> callback = (sender, e) => { };

            // Act
            var result = bootstrap.AddListenerMultiDownloadCompleted(callback);

            // Assert
            Assert.Same(bootstrap, result);
        }

        /// <summary>
        /// Tests that AddListenerMultiDownloadError returns the bootstrap instance for chaining.
        /// </summary>
        [Fact]
        public void AddListenerMultiDownloadError_ReturnsBootstrapInstance()
        {
            // Arrange
            var bootstrap = new GeneralUpdateBootstrap();
            Action<object, MultiDownloadErrorEventArgs> callback = (sender, e) => { };

            // Act
            var result = bootstrap.AddListenerMultiDownloadError(callback);

            // Assert
            Assert.Same(bootstrap, result);
        }

        /// <summary>
        /// Tests that AddListenerMultiDownloadStatistics returns the bootstrap instance for chaining.
        /// </summary>
        [Fact]
        public void AddListenerMultiDownloadStatistics_ReturnsBootstrapInstance()
        {
            // Arrange
            var bootstrap = new GeneralUpdateBootstrap();
            Action<object, MultiDownloadStatisticsEventArgs> callback = (sender, e) => { };

            // Act
            var result = bootstrap.AddListenerMultiDownloadStatistics(callback);

            // Assert
            Assert.Same(bootstrap, result);
        }

        /// <summary>
        /// Tests that AddListenerException returns the bootstrap instance for chaining.
        /// </summary>
        [Fact]
        public void AddListenerException_ReturnsBootstrapInstance()
        {
            // Arrange
            var bootstrap = new GeneralUpdateBootstrap();
            Action<object, ExceptionEventArgs> callback = (sender, e) => { };

            // Act
            var result = bootstrap.AddListenerException(callback);

            // Assert
            Assert.Same(bootstrap, result);
        }

        /// <summary>
        /// Tests that null callback throws ArgumentNullException.
        /// </summary>
        [Fact]
        public void AddListener_WithNullCallback_ThrowsArgumentNullException()
        {
            // Arrange
            var bootstrap = new GeneralUpdateBootstrap();
            Action<object, ExceptionEventArgs>? callback = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => bootstrap.AddListenerException(callback!));
        }

        /// <summary>
        /// Tests method chaining pattern for configuration.
        /// </summary>
        [Fact]
        public void MethodChaining_ConfigureBootstrap_ReturnsCorrectInstance()
        {
            // Arrange & Act
            var bootstrap = new GeneralUpdateBootstrap()
                .SetConfig(new Configinfo
                {
                    InstallPath = "/test/install",
                    MainAppName = "TestApp.exe",
                    UpdateUrl = "https://example.com/update",
                    ClientVersion = "1.0.0"
                })
                .SetCustomSkipOption(() => false)
                .AddListenerException((sender, e) => { });

            // Assert
            Assert.NotNull(bootstrap);
        }
    }
}
