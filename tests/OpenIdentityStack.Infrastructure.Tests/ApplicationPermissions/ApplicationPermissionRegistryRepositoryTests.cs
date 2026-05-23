using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Application.Common;
using OpenIdentityStack.Application.ApplicationPermissions.Dtos;
using OpenIdentityStack.Application.ApplicationPermissions.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ApplicationPermissions;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Persistence.ApplicationPermissions;
using OpenIdentityStack.Infrastructure.Tests.Common;

namespace OpenIdentityStack.Infrastructure.Tests.ApplicationPermissions;

public sealed class ApplicationPermissionRegistryRepositoryTests : IClassFixture<SqliteTestFixture>, IAsyncLifetime
{
    private readonly SqliteTestFixture fixture;
    private readonly IDateTimeProvider dateTimeProvider;
    private OpenIdentityStackDbContext dbContext = null!;
    private ApplicationPermissionRegistryRepository repository = null!;

    public ApplicationPermissionRegistryRepositoryTests(SqliteTestFixture fixture)
    {
        this.fixture = fixture;
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2026, 1, 18, 12, 0, 0, TimeSpan.Zero));
    }

    public async ValueTask InitializeAsync()
    {
        await this.fixture.ClearAllDataAsync();
        this.dbContext = this.fixture.CreateDbContext();
        this.repository = new ApplicationPermissionRegistryRepository(this.dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await this.dbContext.DisposeAsync();
    }

    [Fact]
    public async Task ListApplicationsAsync_ReturnsPermissionCountsWithoutLoadingPermissionEntities()
    {
        RegisteredApplication application = CreateApplication(
            "orders-api",
            [
                ("read-orders", "Read orders", null, null, null),
                ("write-orders", "Write orders", null, null, null),
            ]);
        await this.repository.AddAsync(application);
        await this.repository.SaveChangesAsync();
        this.dbContext.ChangeTracker.Clear();

        PagedResult<RegisteredApplicationSummaryDto> result = await this.repository.ListApplicationsAsync(new ListRegisteredApplicationsQuery());

        result.TotalCount.ShouldBe(1);
        result.Items.ShouldHaveSingleItem();
        result.Items[0].ApplicationIdentifier.ShouldBe("orders-api");
        result.Items[0].PermissionCount.ShouldBe(2);
        this.dbContext.ChangeTracker.Entries<ApplicationPermission>().ShouldBeEmpty();
    }

    private RegisteredApplication CreateApplication(
        string applicationIdentifier,
        IReadOnlyList<(string Key, string DisplayName, string? Description, string? IntendedUse, string? DocUrl)> permissions)
    {
        Result<RegisteredApplication> result = RegisteredApplication.Register(
            applicationIdentifier,
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
