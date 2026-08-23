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
                ("order:read", "Read orders", null, null),
                ("order:write", "Write orders", null, null),
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

    [Fact]
    public async Task ListApplicationsAsync_ExcludesDeletedApplicationsAndRemovedPermissionsFromCounts()
    {
        RegisteredApplication activeApplication = CreateApplication(
            "orders-api",
            [
                ("order:read", "Read orders", null, null),
                ("order:write", "Write orders", null, null),
            ]);
        ApplicationPermission removedPermission = activeApplication.Permissions.Single(permission => permission.PermissionKey == "order:write");
        activeApplication.RemovePermission(
            removedPermission.Id,
            "admin-1",
            "Permission is no longer supported.",
            this.dateTimeProvider).IsSuccess.ShouldBeTrue();

        RegisteredApplication deletedApplication = CreateApplication(
            "billing-api",
            [
                ("invoice:read", "Read invoices", null, null),
            ]);
        deletedApplication.MarkDeleted("admin-1", "Application retired.", this.dateTimeProvider);

        await this.repository.AddAsync(activeApplication);
        await this.repository.AddAsync(deletedApplication);
        await this.repository.SaveChangesAsync();
        this.dbContext.ChangeTracker.Clear();

        PagedResult<RegisteredApplicationSummaryDto> result = await this.repository.ListApplicationsAsync(new ListRegisteredApplicationsQuery());

        result.TotalCount.ShouldBe(1);
        result.Items.ShouldHaveSingleItem();
        result.Items[0].ApplicationIdentifier.ShouldBe("orders-api");
        result.Items[0].PermissionCount.ShouldBe(1);
    }

    [Fact]
    public async Task ListAssignablePermissionCatalogAsync_ExcludesRemovedPermissionsAndDeletedApplications()
    {
        RegisteredApplication activeApplication = CreateApplication(
            "orders-api",
            [
                ("order:read", "Read orders", null, null),
                ("order:write", "Write orders", null, null),
            ]);
        ApplicationPermission removedPermission = activeApplication.Permissions.Single(permission => permission.PermissionKey == "order:write");
        activeApplication.RemovePermission(
            removedPermission.Id,
            "admin-1",
            "Permission is no longer supported.",
            this.dateTimeProvider).IsSuccess.ShouldBeTrue();

        RegisteredApplication deletedApplication = CreateApplication(
            "billing-api",
            [
                ("invoice:read", "Read invoices", null, null),
            ]);
        deletedApplication.MarkDeleted("admin-1", "Application retired.", this.dateTimeProvider);

        await this.repository.AddAsync(activeApplication);
        await this.repository.AddAsync(deletedApplication);
        await this.repository.SaveChangesAsync();
        this.dbContext.ChangeTracker.Clear();

        IReadOnlyList<ApplicationPermissionDto> result = await this.repository.ListAssignablePermissionCatalogAsync(new ListAssignablePermissionCatalogQuery());

        result.Select(item => item.FullPermissionKey).ShouldBe(
            [
                "orders-api:order:read",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task IsPermissionAssignableAsync_ReturnsFalseForRemovedPermissionsAndDeletedApplications()
    {
        RegisteredApplication activeApplication = CreateApplication(
            "orders-api",
            [
                ("order:read", "Read orders", null, null),
                ("order:write", "Write orders", null, null),
            ]);
        ApplicationPermission removedPermission = activeApplication.Permissions.Single(permission => permission.PermissionKey == "order:write");
        activeApplication.RemovePermission(
            removedPermission.Id,
            "admin-1",
            "Permission is no longer supported.",
            this.dateTimeProvider).IsSuccess.ShouldBeTrue();

        RegisteredApplication deletedApplication = CreateApplication(
            "billing-api",
            [
                ("invoice:read", "Read invoices", null, null),
            ]);
        deletedApplication.MarkDeleted("admin-1", "Application retired.", this.dateTimeProvider);

        await this.repository.AddAsync(activeApplication);
        await this.repository.AddAsync(deletedApplication);
        await this.repository.SaveChangesAsync();
        this.dbContext.ChangeTracker.Clear();

        (await this.repository.IsPermissionAssignableAsync("orders-api:order:read")).ShouldBeTrue();
        (await this.repository.IsPermissionAssignableAsync("orders-api:order:write")).ShouldBeFalse();
        (await this.repository.IsPermissionAssignableAsync("billing-api:invoice:read")).ShouldBeFalse();
    }

    [Fact]
    public async Task TombstoneMetadata_RoundTripsThroughPersistence()
    {
        RegisteredApplication application = CreateApplication(
            "orders-api",
            [
                ("order:read", "Read orders", null, null),
            ]);
        ApplicationPermission permission = application.Permissions.Single();
        application.RemovePermission(
            permission.Id,
            "admin-1",
            "Permission is no longer supported.",
            this.dateTimeProvider).IsSuccess.ShouldBeTrue();
        application.MarkDeleted("admin-1", "Application retired.", this.dateTimeProvider);

        await this.repository.AddAsync(application);
        await this.repository.SaveChangesAsync();
        this.dbContext.ChangeTracker.Clear();

        RegisteredApplication? persisted = await this.repository.GetByIdentifierAsync("orders-api");

        persisted.ShouldNotBeNull();
        persisted.IsDeleted.ShouldBeTrue();
        persisted.DeletedAt.ShouldBe(this.dateTimeProvider.UtcNow);
        persisted.DeletedBy.ShouldBe("admin-1");
        persisted.DeleteReason.ShouldBe("Application retired.");
        ApplicationPermission persistedPermission = persisted.Permissions.Single();
        persistedPermission.IsRemoved.ShouldBeTrue();
        persistedPermission.RemovedAt.ShouldBe(this.dateTimeProvider.UtcNow);
        persistedPermission.RemovedBy.ShouldBe("admin-1");
        persistedPermission.RemoveReason.ShouldBe("Permission is no longer supported.");
    }

    private RegisteredApplication CreateApplication(
        string applicationIdentifier,
        IReadOnlyList<(string Key, string DisplayName, string? Description, string? Category)> permissions)
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
