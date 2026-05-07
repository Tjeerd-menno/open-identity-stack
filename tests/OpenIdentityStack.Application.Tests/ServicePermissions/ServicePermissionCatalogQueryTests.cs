using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Common;
using OpenIdentityStack.Application.ServicePermissions.Dtos;
using OpenIdentityStack.Application.ServicePermissions.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ServicePermissions;

namespace OpenIdentityStack.Application.Tests.ServicePermissions;

public sealed class ServicePermissionCatalogQueryTests
{
    private readonly IServicePermissionRegistryRepository repository;
    private readonly IDateTimeProvider dateTimeProvider;

    public ServicePermissionCatalogQueryTests()
    {
        this.repository = Substitute.For<IServicePermissionRegistryRepository>();
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2026, 1, 18, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task ListAssignablePermissionCatalog_MapsPermissionsToDtos()
    {
        ServicePermission permission = CreateService().Permissions[0];
        var paged = PagedResult<ServicePermission>.Create([permission], 1, 50, 1);
        this.repository.ListAssignablePermissionsAsync(Arg.Any<ListAssignablePermissionCatalogQuery>(), Arg.Any<CancellationToken>()).Returns(paged);
        var handler = new ListAssignablePermissionCatalogQueryHandler(this.repository);

        PagedResult<ServicePermissionDto> result = await handler.HandleAsync(new ListAssignablePermissionCatalogQuery());

        result.TotalCount.ShouldBe(1);
        result.Items[0].FullPermissionKey.ShouldBe("orders-api:read-orders");
        result.Items[0].IsAssignable.ShouldBeTrue();
    }

    [Fact]
    public async Task ListRegisteredServices_MapsServicesToSummaryDtos()
    {
        var summary = new RegisteredServiceSummaryDto(
            Guid.NewGuid(),
            "orders-api",
            "Orders API",
            "owner-1",
            "Active",
            1,
            this.dateTimeProvider.UtcNow,
            null);
        var paged = PagedResult<RegisteredServiceSummaryDto>.Create([summary], 1, 20, 1);
        this.repository.ListServicesAsync(Arg.Any<ListRegisteredServicesQuery>(), Arg.Any<CancellationToken>()).Returns(paged);
        var handler = new ListRegisteredServicesQueryHandler(this.repository);

        PagedResult<RegisteredServiceSummaryDto> result = await handler.HandleAsync(new ListRegisteredServicesQuery());

        result.TotalCount.ShouldBe(1);
        result.Items[0].ServiceIdentifier.ShouldBe("orders-api");
        result.Items[0].PermissionCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetRegisteredService_WhenNotFound_ReturnsNotFound()
    {
        this.repository.GetByIdAsync(Arg.Any<RegisteredServiceId>(), Arg.Any<CancellationToken>()).Returns((RegisteredService?)null);
        var handler = new GetRegisteredServiceQueryHandler(this.repository);

        Result<RegisteredServiceDto> result = await handler.HandleAsync(new GetRegisteredServiceQuery(Guid.NewGuid()));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("NotFound.RegisteredService.NotFound");
    }

    private RegisteredService CreateService()
    {
        Result<RegisteredService> result = RegisteredService.Register(
            "orders-api",
            "Orders API",
            null,
            "owner-1",
            OwnerType.User,
            [("read-orders", "Read orders", null, null, null)],
            "actor-1",
            this.dateTimeProvider);
        return result.Value;
    }
}
