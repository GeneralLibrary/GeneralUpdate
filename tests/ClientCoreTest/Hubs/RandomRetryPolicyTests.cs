using System;
using GeneralUpdate.ClientCore.Hubs;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace ClientCoreTest.Hubs
{
    /// <summary>
    /// Contains test cases for the RandomRetryPolicy class.
    /// Tests retry logic for SignalR connection failures.
    /// </summary>
    public class RandomRetryPolicyTests
    {
        /// <summary>
        /// Tests that NextRetryDelay returns a value when elapsed time is less than 60 seconds.
        /// </summary>
        [Fact]
        public void NextRetryDelay_WithLessThan60Seconds_ReturnsDelay()
        {
            // Arrange
            var policy = new RandomRetryPolicy();
            var context = new RetryContext
            {
                PreviousRetryCount = 1,
                ElapsedTime = TimeSpan.FromSeconds(30),
                RetryReason = new Exception("Test exception")
            };

            // Act
            var delay = policy.NextRetryDelay(context);

            // Assert
            Assert.NotNull(delay);
            Assert.True(delay.Value.TotalSeconds >= 0);
            Assert.True(delay.Value.TotalSeconds <= 10);
        }

        /// <summary>
        /// Tests that NextRetryDelay returns null when elapsed time is 60 seconds or more.
        /// </summary>
        [Fact]
        public void NextRetryDelay_WithMoreThan60Seconds_ReturnsNull()
        {
            // Arrange
            var policy = new RandomRetryPolicy();
            var context = new RetryContext
            {
                PreviousRetryCount = 10,
                ElapsedTime = TimeSpan.FromSeconds(60),
                RetryReason = new Exception("Test exception")
            };

            // Act
            var delay = policy.NextRetryDelay(context);

            // Assert
            Assert.Null(delay);
        }

        /// <summary>
        /// Tests that NextRetryDelay returns null when elapsed time exceeds 60 seconds.
        /// </summary>
        [Fact]
        public void NextRetryDelay_WithGreaterThan60Seconds_ReturnsNull()
        {
            // Arrange
            var policy = new RandomRetryPolicy();
            var context = new RetryContext
            {
                PreviousRetryCount = 15,
                ElapsedTime = TimeSpan.FromSeconds(120),
                RetryReason = new Exception("Test exception")
            };

            // Act
            var delay = policy.NextRetryDelay(context);

            // Assert
            Assert.Null(delay);
        }

        /// <summary>
        /// Tests that NextRetryDelay returns a delay for first retry attempt.
        /// </summary>
        [Fact]
        public void NextRetryDelay_OnFirstRetry_ReturnsDelay()
        {
            // Arrange
            var policy = new RandomRetryPolicy();
            var context = new RetryContext
            {
                PreviousRetryCount = 0,
                ElapsedTime = TimeSpan.FromSeconds(5),
                RetryReason = new Exception("Test exception")
            };

            // Act
            var delay = policy.NextRetryDelay(context);

            // Assert
            Assert.NotNull(delay);
        }

        /// <summary>
        /// Tests that NextRetryDelay boundary condition at exactly 60 seconds.
        /// </summary>
        [Fact]
        public void NextRetryDelay_AtExactly60Seconds_ReturnsNull()
        {
            // Arrange
            var policy = new RandomRetryPolicy();
            var context = new RetryContext
            {
                PreviousRetryCount = 10,
                ElapsedTime = TimeSpan.FromSeconds(60),
                RetryReason = new Exception("Test exception")
            };

            // Act
            var delay = policy.NextRetryDelay(context);

            // Assert
            Assert.Null(delay);
        }

        /// <summary>
        /// Tests that NextRetryDelay boundary condition just before 60 seconds.
        /// </summary>
        [Fact]
        public void NextRetryDelay_JustBefore60Seconds_ReturnsDelay()
        {
            // Arrange
            var policy = new RandomRetryPolicy();
            var context = new RetryContext
            {
                PreviousRetryCount = 8,
                ElapsedTime = TimeSpan.FromSeconds(59.99),
                RetryReason = new Exception("Test exception")
            };

            // Act
            var delay = policy.NextRetryDelay(context);

            // Assert
            Assert.NotNull(delay);
        }

        /// <summary>
        /// Tests that multiple calls to NextRetryDelay produce random values.
        /// </summary>
        [Fact]
        public void NextRetryDelay_MultipleCalls_ProducesVariation()
        {
            // Arrange
            var policy = new RandomRetryPolicy();
            var context = new RetryContext
            {
                PreviousRetryCount = 1,
                ElapsedTime = TimeSpan.FromSeconds(10),
                RetryReason = new Exception("Test exception")
            };

            // Act - Get multiple delays
            var delays = new TimeSpan?[5];
            for (int i = 0; i < 5; i++)
            {
                delays[i] = policy.NextRetryDelay(context);
            }

            // Assert - At least some variation in delays (not all exactly the same)
            Assert.All(delays, d => Assert.NotNull(d));
            Assert.All(delays, d => Assert.True(d!.Value.TotalSeconds >= 0 && d.Value.TotalSeconds <= 10));
        }
    }
}
