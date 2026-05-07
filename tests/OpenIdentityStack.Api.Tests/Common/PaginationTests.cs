
using OpenIdentityStack.Api.Common.Models;

namespace OpenIdentityStack.Api.Tests.Common;
/// <summary>
/// Unit tests for PagedRequest, PagedResponse, and PagedResponseFactory.
/// </summary>
public sealed class PaginationTests
{
    public sealed class PagedRequestTests
    {
        [Fact]
        public void Page_DefaultsToOne()
        {
            // Arrange & Act
            var request = new PagedRequest();

            // Assert
            request.Page.ShouldBe(1);
        }

        [Fact]
        public void PageSize_DefaultsToTwenty()
        {
            // Arrange & Act
            var request = new PagedRequest();

            // Assert
            request.PageSize.ShouldBe(20);
        }

        [Fact]
        public void Page_LessThanOne_DefaultsToOne()
        {
            // Arrange & Act
            var request = new PagedRequest { Page = 0 };

            // Assert
            request.Page.ShouldBe(1);
        }

        [Fact]
        public void Page_NegativeValue_DefaultsToOne()
        {
            // Arrange & Act
            var request = new PagedRequest { Page = -5 };

            // Assert
            request.Page.ShouldBe(1);
        }

        [Fact]
        public void Page_ValidValue_IsPreserved()
        {
            // Arrange & Act
            var request = new PagedRequest { Page = 5 };

            // Assert
            request.Page.ShouldBe(5);
        }

        [Fact]
        public void PageSize_LessThanOne_DefaultsToTwenty()
        {
            // Arrange & Act
            var request = new PagedRequest { PageSize = 0 };

            // Assert
            request.PageSize.ShouldBe(20);
        }

        [Fact]
        public void PageSize_NegativeValue_DefaultsToTwenty()
        {
            // Arrange & Act
            var request = new PagedRequest { PageSize = -10 };

            // Assert
            request.PageSize.ShouldBe(20);
        }

        [Fact]
        public void PageSize_ExceedsMaximum_ClampsToOneHundred()
        {
            // Arrange & Act
            var request = new PagedRequest { PageSize = 200 };

            // Assert
            request.PageSize.ShouldBe(100);
        }

        [Fact]
        public void PageSize_AtMaximum_IsPreserved()
        {
            // Arrange & Act
            var request = new PagedRequest { PageSize = 100 };

            // Assert
            request.PageSize.ShouldBe(100);
        }

        [Fact]
        public void PageSize_ValidValue_IsPreserved()
        {
            // Arrange & Act
            var request = new PagedRequest { PageSize = 50 };

            // Assert
            request.PageSize.ShouldBe(50);
        }

        [Fact]
        public void Skip_FirstPage_ReturnsZero()
        {
            // Arrange
            var request = new PagedRequest { Page = 1, PageSize = 20 };

            // Act & Assert
            request.Skip.ShouldBe(0);
        }

        [Fact]
        public void Skip_SecondPage_ReturnsPageSize()
        {
            // Arrange
            var request = new PagedRequest { Page = 2, PageSize = 20 };

            // Act & Assert
            request.Skip.ShouldBe(20);
        }

        [Fact]
        public void Skip_ThirdPage_ReturnsTwoTimesPageSize()
        {
            // Arrange
            var request = new PagedRequest { Page = 3, PageSize = 25 };

            // Act & Assert
            request.Skip.ShouldBe(50);
        }

        [Fact]
        public void SortBy_DefaultsToNull()
        {
            // Arrange & Act
            var request = new PagedRequest();

            // Assert
            request.SortBy.ShouldBeNull();
        }

        [Fact]
        public void SortBy_CanBeSet()
        {
            // Arrange & Act
            var request = new PagedRequest { SortBy = "Name" };

            // Assert
            request.SortBy.ShouldBe("Name");
        }

        [Fact]
        public void SortDescending_DefaultsToFalse()
        {
            // Arrange & Act
            var request = new PagedRequest();

            // Assert
            request.SortDescending.ShouldBeFalse();
        }

        [Fact]
        public void SortDescending_CanBeSetToTrue()
        {
            // Arrange & Act
            var request = new PagedRequest { SortDescending = true };

            // Assert
            request.SortDescending.ShouldBeTrue();
        }
    }

    public sealed class PagedResponseTests
    {
        [Fact]
        public void TotalPages_WithExactMultiple_ReturnsCorrectCount()
        {
            // Arrange
            var response = new PagedResponse<string>
            {
                Items = ["a", "b"],
                Page = 1,
                PageSize = 10,
                TotalCount = 30
            };

            // Assert
            response.TotalPages.ShouldBe(3);
        }

        [Fact]
        public void TotalPages_WithPartialLastPage_RoundsUp()
        {
            // Arrange
            var response = new PagedResponse<string>
            {
                Items = ["a"],
                Page = 1,
                PageSize = 10,
                TotalCount = 25
            };

            // Assert
            response.TotalPages.ShouldBe(3);
        }

        [Fact]
        public void TotalPages_WithZeroItems_ReturnsZero()
        {
            // Arrange
            var response = new PagedResponse<string>
            {
                Items = [],
                Page = 1,
                PageSize = 10,
                TotalCount = 0
            };

            // Assert
            response.TotalPages.ShouldBe(0);
        }

        [Fact]
        public void HasPreviousPage_FirstPage_ReturnsFalse()
        {
            // Arrange
            var response = new PagedResponse<string>
            {
                Items = [],
                Page = 1,
                PageSize = 10,
                TotalCount = 50
            };

            // Assert
            response.HasPreviousPage.ShouldBeFalse();
        }

        [Fact]
        public void HasPreviousPage_SecondPage_ReturnsTrue()
        {
            // Arrange
            var response = new PagedResponse<string>
            {
                Items = [],
                Page = 2,
                PageSize = 10,
                TotalCount = 50
            };

            // Assert
            response.HasPreviousPage.ShouldBeTrue();
        }

        [Fact]
        public void HasNextPage_LastPage_ReturnsFalse()
        {
            // Arrange
            var response = new PagedResponse<string>
            {
                Items = [],
                Page = 5,
                PageSize = 10,
                TotalCount = 50
            };

            // Assert
            response.HasNextPage.ShouldBeFalse();
        }

        [Fact]
        public void HasNextPage_NotLastPage_ReturnsTrue()
        {
            // Arrange
            var response = new PagedResponse<string>
            {
                Items = [],
                Page = 3,
                PageSize = 10,
                TotalCount = 50
            };

            // Assert
            response.HasNextPage.ShouldBeTrue();
        }

        [Fact]
        public void HasNextPage_OnlyOnePage_ReturnsFalse()
        {
            // Arrange
            var response = new PagedResponse<string>
            {
                Items = [],
                Page = 1,
                PageSize = 10,
                TotalCount = 5
            };

            // Assert
            response.HasNextPage.ShouldBeFalse();
        }
    }

    public sealed class PagedResponseFactoryTests
    {
        [Fact]
        public void Create_SetsAllPropertiesCorrectly()
        {
            // Arrange
            var items = new List<string> { "a", "b", "c" };

            // Act
            PagedResponse<string> response = PagedResponseFactory.Create(items, page: 2, pageSize: 10, totalCount: 50);

            // Assert
            response.Items.ShouldBe(items);
            response.Page.ShouldBe(2);
            response.PageSize.ShouldBe(10);
            response.TotalCount.ShouldBe(50);
        }

        [Fact]
        public void Create_CalculatesTotalPagesCorrectly()
        {
            // Arrange
            var items = new List<int> { 1, 2, 3 };

            // Act
            PagedResponse<int> response = PagedResponseFactory.Create(items, page: 1, pageSize: 10, totalCount: 35);

            // Assert
            response.TotalPages.ShouldBe(4);
        }

        [Fact]
        public void Empty_ReturnsEmptyItems()
        {
            // Act
            PagedResponse<string> response = PagedResponseFactory.Empty<string>();

            // Assert
            response.Items.ShouldBeEmpty();
        }

        [Fact]
        public void Empty_WithDefaultParameters_SetsPageToOne()
        {
            // Act
            PagedResponse<string> response = PagedResponseFactory.Empty<string>();

            // Assert
            response.Page.ShouldBe(1);
        }

        [Fact]
        public void Empty_WithDefaultParameters_SetsPageSizeToTwenty()
        {
            // Act
            PagedResponse<string> response = PagedResponseFactory.Empty<string>();

            // Assert
            response.PageSize.ShouldBe(20);
        }

        [Fact]
        public void Empty_SetsTotalCountToZero()
        {
            // Act
            PagedResponse<string> response = PagedResponseFactory.Empty<string>();

            // Assert
            response.TotalCount.ShouldBe(0);
        }

        [Fact]
        public void Empty_WithCustomParameters_UsesProvidedValues()
        {
            // Act
            PagedResponse<int> response = PagedResponseFactory.Empty<int>(page: 5, pageSize: 50);

            // Assert
            response.Page.ShouldBe(5);
            response.PageSize.ShouldBe(50);
        }

        [Fact]
        public void Empty_TotalPages_ReturnsZero()
        {
            // Act
            PagedResponse<string> response = PagedResponseFactory.Empty<string>();

            // Assert
            response.TotalPages.ShouldBe(0);
        }

        [Fact]
        public void Empty_HasPreviousPage_ReturnsFalse()
        {
            // Act
            PagedResponse<string> response = PagedResponseFactory.Empty<string>();

            // Assert
            response.HasPreviousPage.ShouldBeFalse();
        }

        [Fact]
        public void Empty_HasNextPage_ReturnsFalse()
        {
            // Act
            PagedResponse<string> response = PagedResponseFactory.Empty<string>();

            // Assert
            response.HasNextPage.ShouldBeFalse();
        }
    }
}
