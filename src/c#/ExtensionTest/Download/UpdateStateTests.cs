using GeneralUpdate.Extension.Download;
using Xunit;

namespace ExtensionTest.Download
{
    /// <summary>
    /// Contains test cases for the UpdateState enum.
    /// Tests the different lifecycle states of an extension update operation.
    /// </summary>
    public class UpdateStateTests
    {
        /// <summary>
        /// Tests that Queued state has a value of 0.
        /// </summary>
        [Fact]
        public void Queued_ShouldHaveValueZero()
        {
            // Arrange & Act
            var state = UpdateState.Queued;

            // Assert
            Assert.Equal(0, (int)state);
        }

        /// <summary>
        /// Tests that Updating state has a value of 1.
        /// </summary>
        [Fact]
        public void Updating_ShouldHaveValueOne()
        {
            // Arrange & Act
            var state = UpdateState.Updating;

            // Assert
            Assert.Equal(1, (int)state);
        }

        /// <summary>
        /// Tests that UpdateSuccessful state has a value of 2.
        /// </summary>
        [Fact]
        public void UpdateSuccessful_ShouldHaveValueTwo()
        {
            // Arrange & Act
            var state = UpdateState.UpdateSuccessful;

            // Assert
            Assert.Equal(2, (int)state);
        }

        /// <summary>
        /// Tests that UpdateFailed state has a value of 3.
        /// </summary>
        [Fact]
        public void UpdateFailed_ShouldHaveValueThree()
        {
            // Arrange & Act
            var state = UpdateState.UpdateFailed;

            // Assert
            Assert.Equal(3, (int)state);
        }

        /// <summary>
        /// Tests that Cancelled state has a value of 4.
        /// </summary>
        [Fact]
        public void Cancelled_ShouldHaveValueFour()
        {
            // Arrange & Act
            var state = UpdateState.Cancelled;

            // Assert
            Assert.Equal(4, (int)state);
        }

        /// <summary>
        /// Tests that all UpdateState values are unique.
        /// </summary>
        [Fact]
        public void AllStates_ShouldHaveUniqueValues()
        {
            // Arrange
            var states = new[]
            {
                UpdateState.Queued,
                UpdateState.Updating,
                UpdateState.UpdateSuccessful,
                UpdateState.UpdateFailed,
                UpdateState.Cancelled
            };

            // Act
            var uniqueCount = states.Distinct().Count();

            // Assert
            Assert.Equal(5, uniqueCount);
        }

        /// <summary>
        /// Tests that UpdateState can be converted to and from its underlying integer value.
        /// </summary>
        [Fact]
        public void UpdateState_CanBeConvertedToAndFromInteger()
        {
            // Arrange
            var originalState = UpdateState.Updating;
            var intValue = (int)originalState;

            // Act
            var convertedState = (UpdateState)intValue;

            // Assert
            Assert.Equal(originalState, convertedState);
        }
    }
}
