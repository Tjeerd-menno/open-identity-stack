using OpenIdentityStack.Application.Common;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Common;

public sealed class PagedResultTests
{
    [Fact]
    public void Create_WithValidInputs_ComputesPaginationProperties()
    {
        // Arrange
        IReadOnlyList<string> items = new[] { "a", "b" };

        // Act
        var result = PagedResult<string>.Create(items, page: 2, pageSize: 2, totalCount: 5);

        // Assert
        result.Items.ShouldBe(items);
        result.Page.ShouldBe(2);
        result.PageSize.ShouldBe(2);
        result.TotalCount.ShouldBe(5);
        result.TotalPages.ShouldBe(3);
        result.HasPreviousPage.ShouldBeTrue();
        result.HasNextPage.ShouldBeTrue();
    }

    [Fact]
    public void Empty_WithDefaults_ReturnsEmptyResultWithoutPagination()
    {
        // Act
        var result = PagedResult<int>.Empty();

        // Assert
        result.Items.ShouldBeEmpty();
        result.Page.ShouldBe(1);
        result.PageSize.ShouldBe(20);
        result.TotalCount.ShouldBe(0);
        result.TotalPages.ShouldBe(0);
        result.HasPreviousPage.ShouldBeFalse();
        result.HasNextPage.ShouldBeFalse();
    }

    [Fact]
    public void TotalPages_WithZeroPageSize_ReturnsZero()
    {
        // Act
        var result = PagedResult<string>.Create([], page: 1, pageSize: 0, totalCount: 10);

        // Assert
        result.TotalPages.ShouldBe(0);
        result.HasNextPage.ShouldBeFalse();
    }
}
