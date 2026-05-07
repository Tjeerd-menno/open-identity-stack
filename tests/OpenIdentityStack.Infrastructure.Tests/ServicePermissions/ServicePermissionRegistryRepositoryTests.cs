using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Application.Common;
using OpenIdentityStack.Application.ServicePermissions.Dtos;
using OpenIdentityStack.Application.ServicePermissions.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ServicePermissions;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Persistence.ServicePermissions;
using OpenIdentityStack.Infrastructure.Tests.Common;

namespace OpenIdentityStack.Infrastructure.Tests.ServicePermissions;

public sealed class ServicePermissionRegistryRepositoryTests : IClassFixture<SqliteTestFixture>, IAsyncLifetime
{
    private readonly SqliteTestFixture fixture;
    private readonly IDateTimeProvider dateTimeProvider;
    private OpenIdentityStackDbContext dbContext = null!;
    private ServicePermissionRegistryRepository repository = null!;

    public ServicePermissionRegistryRepositoryTests(SqliteTestFixture fixture)
    {
        this.fixture = fixture;
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2026, 1, 18, 12, 0, 0, TimeSpan.Zero));
    }

    public async ValueTask InitializeAsync()
    {
        await this.fixture.ClearAllDataAsync();
        this.dbContext = this.fixture.CreateDbContext();
        this.repository = new ServicePermissionRegistryRepository(this.dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await this.dbContext.DisposeAsync();
    }

    [Fact]
    public async Task ListServicesAsync_ReturnsPermissionCountsWithoutLoadingPermissionEntities()
    {
        RegisteredService service = CreateService(
            "orders-api",
            [
                ("read-orders", "Read orders", null, null, null),
                ("write-orders", "Write orders", null, null, null),
            ]);
        await this.repository.AddAsync(service);
        await this.repository.SaveChangesAsync();
        this.dbContext.ChangeTracker.Clear();

        PagedResult<RegisteredServiceSummaryDto> result = await this.repository.ListServicesAsync(new ListRegisteredServicesQuery());

        result.TotalCount.ShouldBe(1);
        result.Items.ShouldHaveSingleItem();
        result.Items[0].ServiceIdentifier.ShouldBe("orders-api");
        result.Items[0].PermissionCount.ShouldBe(2);
        this.dbContext.ChangeTracker.Entries<ServicePermission>().ShouldBeEmpty();
    }

    private RegisteredService CreateService(
        string serviceIdentifier,
        IReadOnlyList<(string Key, string DisplayName, string? Description, string? IntendedUse, string? DocUrl)> permissions)
    {
        Result<RegisteredService> result = RegisteredService.Register(
            serviceIdentifier,
            "Orders API",
            null,
            "owner-1",
            OwnerType.User,
            permissions,
            "actor-1",
            this.dateTimeProvider);

        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }
}
