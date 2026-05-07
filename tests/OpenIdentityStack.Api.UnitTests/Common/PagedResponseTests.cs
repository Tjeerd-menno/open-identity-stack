using OpenIdentityStack.Api.Common.Models;

namespace OpenIdentityStack.Api.UnitTests.Common;

public sealed class PagedResponseTests
{
    [Fact]
    public void TotalPages_CalculatesCorrectly_ExactDivision()
    {
        var response = new PagedResponse<string>
        {
            Items = [],
            Page = 1,
            PageSize = 10,
            TotalCount = 30
        };
        response.TotalPages.ShouldBe(3);
    }

    [Fact]
    public void TotalPages_CalculatesCorrectly_WithRemainder()
    {
        var response = new PagedResponse<string>
        {
            Items = [],
            Page = 1,
            PageSize = 10,
            TotalCount = 25
        };
        response.TotalPages.ShouldBe(3);
    }

    [Fact]
    public void TotalPages_Zero_Items_Returns_Zero()
    {
        var response = new PagedResponse<string>
        {
            Items = [],
            Page = 1,
            PageSize = 20,
            TotalCount = 0
        };
        response.TotalPages.ShouldBe(0);
    }

    [Fact]
    public void HasPreviousPage_FirstPage_ReturnsFalse()
    {
        var response = new PagedResponse<string>
        {
            Items = [],
            Page = 1,
            PageSize = 10,
            TotalCount = 30
        };
        response.HasPreviousPage.ShouldBeFalse();
    }

    [Fact]
    public void HasPreviousPage_SecondPage_ReturnsTrue()
    {
        var response = new PagedResponse<string>
        {
            Items = [],
            Page = 2,
            PageSize = 10,
            TotalCount = 30
        };
        response.HasPreviousPage.ShouldBeTrue();
    }

    [Fact]
    public void HasNextPage_LastPage_ReturnsFalse()
    {
        var response = new PagedResponse<string>
        {
            Items = [],
            Page = 3,
            PageSize = 10,
            TotalCount = 30
        };
        response.HasNextPage.ShouldBeFalse();
    }

    [Fact]
    public void HasNextPage_NotLastPage_ReturnsTrue()
    {
        var response = new PagedResponse<string>
        {
            Items = [],
            Page = 1,
            PageSize = 10,
            TotalCount = 30
        };
        response.HasNextPage.ShouldBeTrue();
    }

    [Fact]
    public void Factory_Create_ReturnsCorrectResponse()
    {
        string[] items = ["a", "b", "c"];
        PagedResponse<string> response = PagedResponseFactory.Create<string>(items, page: 2, pageSize: 3, totalCount: 10);

        response.Items.ShouldBe(items);
        response.Page.ShouldBe(2);
        response.PageSize.ShouldBe(3);
        response.TotalCount.ShouldBe(10);
    }

    [Fact]
    public void Factory_Empty_ReturnsEmptyResponse()
    {
        PagedResponse<string> response = PagedResponseFactory.Empty<string>();

        response.Items.ShouldBeEmpty();
        response.Page.ShouldBe(1);
        response.PageSize.ShouldBe(20);
        response.TotalCount.ShouldBe(0);
        response.TotalPages.ShouldBe(0);
        response.HasPreviousPage.ShouldBeFalse();
        response.HasNextPage.ShouldBeFalse();
    }

    [Fact]
    public void Factory_Empty_WithCustomPageAndSize_ReturnsCorrectValues()
    {
        PagedResponse<int> response = PagedResponseFactory.Empty<int>(page: 3, pageSize: 50);

        response.Page.ShouldBe(3);
        response.PageSize.ShouldBe(50);
    }
}
