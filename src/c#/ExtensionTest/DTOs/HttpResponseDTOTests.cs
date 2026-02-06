using GeneralUpdate.Extension.DTOs;
using Xunit;

namespace ExtensionTest.DTOs
{
    /// <summary>
    /// Contains test cases for HttpResponseDTO classes
    /// </summary>
    public class HttpResponseDTOTests
    {
        [Fact]
        public void HttpResponseDTO_Success_ShouldCreateSuccessResponse()
        {
            // Act
            var response = HttpResponseDTO.Success("Operation successful");

            // Assert
            Assert.Equal(200, response.Code);
            Assert.Equal("Operation successful", response.Message);
        }

        [Fact]
        public void HttpResponseDTO_InnerException_ShouldCreateErrorResponse()
        {
            // Act
            var response = HttpResponseDTO.InnerException("An error occurred");

            // Assert
            Assert.Equal(500, response.Code);
            Assert.Equal("An error occurred", response.Message);
        }

        [Fact]
        public void HttpResponseDTOGeneric_Success_ShouldCreateSuccessResponseWithData()
        {
            // Arrange
            var testData = new { Id = 1, Name = "Test" };

            // Act
            var response = HttpResponseDTO<object>.Success(testData, "Data retrieved");

            // Assert
            Assert.Equal(200, response.Code);
            Assert.Equal("Data retrieved", response.Message);
            Assert.NotNull(response.Body);
            Assert.Equal(testData, response.Body);
        }

        [Fact]
        public void HttpResponseDTOGeneric_Failure_ShouldCreateFailureResponse()
        {
            // Act
            var response = HttpResponseDTO<string>.Failure("Validation failed");

            // Assert
            Assert.Equal(400, response.Code);
            Assert.Equal("Validation failed", response.Message);
        }

        [Fact]
        public void HttpResponseDTOGeneric_InnerException_ShouldCreateErrorResponse()
        {
            // Act
            var response = HttpResponseDTO<int>.InnerException("Internal error");

            // Assert
            Assert.Equal(500, response.Code);
            Assert.Equal("Internal error", response.Message);
        }
    }
}
