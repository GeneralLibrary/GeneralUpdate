using System.Collections.Generic;
using System.Linq;
using GeneralUpdate.Extension.DTOs;
using Xunit;

namespace ExtensionTest.DTOs
{
    /// <summary>
    /// Contains test cases for PagedResultDTO
    /// </summary>
    public class PagedResultDTOTests
    {
        [Fact]
        public void PagedResultDTO_HasPrevious_ShouldBeTrueWhenPageNumberGreaterThanOne()
        {
            // Arrange
            var result = new PagedResultDTO<string>
            {
                PageNumber = 2,
                PageSize = 10,
                TotalCount = 30,
                TotalPages = 3,
                Items = new List<string> { "item1", "item2" }
            };

            // Act & Assert
            Assert.True(result.HasPrevious);
        }

        [Fact]
        public void PagedResultDTO_HasPrevious_ShouldBeFalseWhenPageNumberIsOne()
        {
            // Arrange
            var result = new PagedResultDTO<string>
            {
                PageNumber = 1,
                PageSize = 10,
                TotalCount = 30,
                TotalPages = 3,
                Items = new List<string> { "item1", "item2" }
            };

            // Act & Assert
            Assert.False(result.HasPrevious);
        }

        [Fact]
        public void PagedResultDTO_HasNext_ShouldBeTrueWhenPageNumberLessThanTotalPages()
        {
            // Arrange
            var result = new PagedResultDTO<string>
            {
                PageNumber = 2,
                PageSize = 10,
                TotalCount = 30,
                TotalPages = 3,
                Items = new List<string> { "item1", "item2" }
            };

            // Act & Assert
            Assert.True(result.HasNext);
        }

        [Fact]
        public void PagedResultDTO_HasNext_ShouldBeFalseWhenPageNumberEqualsToTotalPages()
        {
            // Arrange
            var result = new PagedResultDTO<string>
            {
                PageNumber = 3,
                PageSize = 10,
                TotalCount = 30,
                TotalPages = 3,
                Items = new List<string> { "item1", "item2" }
            };

            // Act & Assert
            Assert.False(result.HasNext);
        }

        [Fact]
        public void PagedResultDTO_Items_ShouldDefaultToEmptyEnumerable()
        {
            // Arrange & Act
            var result = new PagedResultDTO<string>();

            // Assert
            Assert.NotNull(result.Items);
            Assert.Empty(result.Items);
        }
    }
}
