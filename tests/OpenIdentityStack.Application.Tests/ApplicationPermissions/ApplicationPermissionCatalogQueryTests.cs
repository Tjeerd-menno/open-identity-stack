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
        var paged = PagedResult<ApplicationPermissionDto>.Create([dto], 1, 50, 1);
        this.repository.ListAssignablePermissionCatalogAsync(Arg.Any<ListAssignablePermissionCatalogQuery>(), Arg.Any<CancellationToken>()).Returns(paged);
        var handler = new ListAssignablePermissionCatalogQueryHandler(this.repository);

        PagedResult<ApplicationPermissionDto> result = await handler.HandleAsync(new ListAssignablePermissionCatalogQuery());

        result.TotalCount.ShouldBe(1);
        result.Items[0].FullPermissionKey.ShouldBe("orders-api:read-orders");
        result.Items[0].Category.ShouldBeNull();
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
            [("read-orders", "Read orders", null, null)],
            "actor-1",
            this.dateTimeProvider);
        return result.Value;
    }
}
