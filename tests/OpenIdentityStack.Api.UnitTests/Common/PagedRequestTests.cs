using OpenIdentityStack.Api.Common.Models;

namespace OpenIdentityStack.Api.UnitTests.Common;

public sealed class PagedRequestTests
{
    [Fact]
    public void Page_Defaults_To_One()
    {
        var request = new PagedRequest();
        request.Page.ShouldBe(1);
    }

    [Fact]
    public void PageSize_Defaults_To_Twenty()
    {
        var request = new PagedRequest();
        request.PageSize.ShouldBe(20);
    }

    [Fact]
    public void Page_Below_One_Resets_To_Default()
    {
        var request = new PagedRequest { Page = 0 };
        request.Page.ShouldBe(1);
    }

    [Fact]
    public void Page_Negative_Resets_To_Default()
    {
        var request = new PagedRequest { Page = -5 };
        request.Page.ShouldBe(1);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(100)]
    public void Page_Valid_Values_Are_Preserved(int page)
    {
        var request = new PagedRequest { Page = page };
        request.Page.ShouldBe(page);
    }

    [Fact]
    public void PageSize_Below_One_Resets_To_Default()
    {
        var request = new PagedRequest { PageSize = 0 };
        request.PageSize.ShouldBe(20);
    }

    [Fact]
    public void PageSize_Negative_Resets_To_Default()
    {
        var request = new PagedRequest { PageSize = -1 };
        request.PageSize.ShouldBe(20);
    }

    [Fact]
    public void PageSize_Above_Max_Clamped_To_100()
    {
        var request = new PagedRequest { PageSize = 200 };
        request.PageSize.ShouldBe(100);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void PageSize_Valid_Values_Are_Preserved(int pageSize)
    {
        var request = new PagedRequest { PageSize = pageSize };
        request.PageSize.ShouldBe(pageSize);
    }

    [Fact]
    public void Skip_FirstPage_Returns_Zero()
    {
        var request = new PagedRequest { Page = 1, PageSize = 20 };
        request.Skip.ShouldBe(0);
    }

    [Fact]
    public void Skip_SecondPage_Returns_PageSize()
    {
        var request = new PagedRequest { Page = 2, PageSize = 20 };
        request.Skip.ShouldBe(20);
    }

    [Fact]
    public void Skip_ThirdPage_Returns_TwoTimesPageSize()
    {
        var request = new PagedRequest { Page = 3, PageSize = 10 };
        request.Skip.ShouldBe(20);
    }

    [Fact]
    public void SortBy_Defaults_To_Null()
    {
        var request = new PagedRequest();
        request.SortBy.ShouldBeNull();
    }

    [Fact]
    public void SortDescending_Defaults_To_False()
    {
        var request = new PagedRequest();
        request.SortDescending.ShouldBeFalse();
    }
}
