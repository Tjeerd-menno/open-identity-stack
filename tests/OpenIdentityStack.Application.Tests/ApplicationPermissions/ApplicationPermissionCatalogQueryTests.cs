using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Common;
using OpenIdentityStack.Application.ApplicationPermissions.Dtos;
using OpenIdentityStack.Application.ApplicationPermissions.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ApplicationPermissions;

namespace OpenIdentityStack.Application.Tests.ApplicationPermissions;

public sealed class ApplicationPermissionCatalogQueryTests
{
    private readonly IApplicationPermissionRegistryRepository repository;
    private readonly IDateTimeProvider dateTimeProvider;

    public ApplicationPermissionCatalogQueryTests()
    {
        this.repository = Substitute.For<IApplicationPermissionRegistryRepository>();
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2026, 1, 18, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task ListAssignablePermissionCatalog_MapsPermissionsToDtos()
    {
        ApplicationPermission permission = CreateApplication().Permissions[0];
        var dto = new ApplicationPermissionDto(
            permission.Id.Value,
            permission.PermissionKey,
            permission.FullPermissionKey,
            permission.DisplayName,
            permission.Description,
            permission.Category,
            permission.CreatedAt,
            permission.ModifiedAt,
            "orders-api",
            "Orders API",
            null);
        this.repository.ListAssignablePermissionCatalogAsync(Arg.Any<ListAssignablePermissionCatalogQuery>(), Arg.Any<CancellationToken>())
            .Returns([dto]);
        var handler = new ListAssignablePermissionCatalogQueryHandler(this.repository);

        PagedResult<ApplicationPermissionDto> result = await handler.HandleAsync(new ListAssignablePermissionCatalogQuery());

        result.TotalCount.ShouldBe(2);
        result.Items[0].FullPermissionKey.ShouldBe("orders-api:order:*");
        result.Items[1].FullPermissionKey.ShouldBe("orders-api:order:read");
        result.Items[1].Category.ShouldBeNull();
    }

    [Fact]
    public async Task ListAssignablePermissionCatalog_DerivesWildcardRowsFromConcreteCatalog()
    {
        var concrete = new ApplicationPermissionDto(
            Guid.NewGuid(),
            "order-item:read",
            "orders-api:order-item:read",
            "Read order item",
            null,
            "order-item",
            this.dateTimeProvider.UtcNow,
            null,
            "orders-api",
            "Orders API",
            "1.0.0");

        this.repository.ListAssignablePermissionCatalogAsync(Arg.Any<ListAssignablePermissionCatalogQuery>(), Arg.Any<CancellationToken>())
            .Returns([concrete]);

        var handler = new ListAssignablePermissionCatalogQueryHandler(this.repository);

        PagedResult<ApplicationPermissionDto> result = await handler.HandleAsync(
            new ListAssignablePermissionCatalogQuery(Page: 1, PageSize: 50));

        result.TotalCount.ShouldBe(2);
        result.Items.Select(item => item.FullPermissionKey).ShouldBe(
            ["orders-api:order-item:*", "orders-api:order-item:read"],
            ignoreOrder: false);
        result.Items[0].Kind.ShouldBe("wildcard");
        result.Items[0].DisplayName.ShouldBe("Orders API Order Item All");
    }

    [Fact]
    public async Task ListAssignablePermissionCatalog_AppliesWildcardFirstOrderingAndPaging()
    {
        var ordersRead = new ApplicationPermissionDto(
            Guid.NewGuid(),
            "order:read",
            "orders-api:order:read",
            "Read orders",
            null,
            "order",
            this.dateTimeProvider.UtcNow,
            null,
            "orders-api",
            "Orders API",
            "1.0.0");
        var ordersWrite = new ApplicationPermissionDto(
            Guid.NewGuid(),
            "order:write",
            "orders-api:order:write",
            "Write orders",
            null,
            "order",
            this.dateTimeProvider.UtcNow,
            null,
            "orders-api",
            "Orders API",
            "1.0.0");
        var billingRead = new ApplicationPermissionDto(
            Guid.NewGuid(),
            "invoice:read",
            "billing-api:invoice:read",
            "Read invoices",
            null,
            "invoice",
            this.dateTimeProvider.UtcNow,
            null,
            "billing-api",
            "Billing API",
            "1.0.0");

        this.repository.ListAssignablePermissionCatalogAsync(Arg.Any<ListAssignablePermissionCatalogQuery>(), Arg.Any<CancellationToken>())
            .Returns([ordersRead, ordersWrite, billingRead]);

        var handler = new ListAssignablePermissionCatalogQueryHandler(this.repository);

        PagedResult<ApplicationPermissionDto> firstPage = await handler.HandleAsync(
            new ListAssignablePermissionCatalogQuery(Page: 1, PageSize: 2));
        PagedResult<ApplicationPermissionDto> secondPage = await handler.HandleAsync(
            new ListAssignablePermissionCatalogQuery(Page: 2, PageSize: 2));

        firstPage.TotalCount.ShouldBe(5);
        firstPage.Items.Select(item => item.FullPermissionKey).ShouldBe(
            ["billing-api:invoice:*", "billing-api:invoice:read"],
            ignoreOrder: false);
        secondPage.Items.Select(item => item.FullPermissionKey).ShouldBe(
            ["orders-api:order:*", "orders-api:order:read"],
            ignoreOrder: false);
    }

    [Fact]
    public async Task ListRegisteredApplications_MapsApplicationsToSummaryDtos()
    {
        var summary = new RegisteredApplicationSummaryDto(
            Guid.NewGuid(),
            "orders-api",
            "Orders API",
            "owner-1",
            "Active",
            1,
            this.dateTimeProvider.UtcNow,
            null);
        var paged = PagedResult<RegisteredApplicationSummaryDto>.Create([summary], 1, 20, 1);
        this.repository.ListApplicationsAsync(Arg.Any<ListRegisteredApplicationsQuery>(), Arg.Any<CancellationToken>()).Returns(paged);
        var handler = new ListRegisteredApplicationsQueryHandler(this.repository);

        PagedResult<RegisteredApplicationSummaryDto> result = await handler.HandleAsync(new ListRegisteredApplicationsQuery());

        result.TotalCount.ShouldBe(1);
        result.Items[0].ApplicationIdentifier.ShouldBe("orders-api");
        result.Items[0].PermissionCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetRegisteredApplication_WhenNotFound_ReturnsNotFound()
    {
        this.repository.GetByIdAsync(Arg.Any<RegisteredApplicationId>(), Arg.Any<CancellationToken>()).Returns((RegisteredApplication?)null);
        var handler = new GetRegisteredApplicationQueryHandler(this.repository);

        Result<RegisteredApplicationDto> result = await handler.HandleAsync(new GetRegisteredApplicationQuery(Guid.NewGuid()));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("NotFound.RegisteredApplication.NotFound");
    }

    private RegisteredApplication CreateApplication()
    {
        Result<RegisteredApplication> result = RegisteredApplication.Register(
            "orders-api",
            "Orders API",
            null,
            "owner-1",
            OwnerType.User,
            [("order:read", "Read orders", null, null)],
            "actor-1",
            this.dateTimeProvider);
        return result.Value;
    }
}
