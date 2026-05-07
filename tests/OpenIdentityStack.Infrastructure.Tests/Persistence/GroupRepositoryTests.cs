using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Groups;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Persistence.Groups;
using OpenIdentityStack.Infrastructure.Tests.Common;

using SharedKernel;
#pragma warning disable IDE0007 // Use var instead of explicit type
#pragma warning disable IDE0008 // Use explicit type instead of var

namespace OpenIdentityStack.Infrastructure.Tests.Persistence;

public sealed class GroupRepositoryTests : IClassFixture<SqliteTestFixture>, IAsyncLifetime
{
    private readonly SqliteTestFixture _fixture;
    private OpenIdentityStackDbContext _dbContext = null!;
    private GroupRepository _repository = null!;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly DateTimeOffset _now = new(2026, 1, 22, 12, 0, 0, TimeSpan.Zero);

    public GroupRepositoryTests(SqliteTestFixture fixture)
    {
        this._fixture = fixture;
        IDateTimeProvider provider = Substitute.For<IDateTimeProvider>();
        provider.UtcNow.Returns(this._now);
        this._dateTimeProvider = provider;
    }

    public async ValueTask InitializeAsync()
    {
        await this._fixture.ClearAllDataAsync();
        this._dbContext = this._fixture.CreateDbContext();
        this._repository = new GroupRepository(this._dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await this._dbContext.DisposeAsync();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsGroup_WhenExists()
    {
        Group group = await this.SeedAsync("test-group", "Test Group");

        Group? result = await this._repository.GetByIdAsync(group.Id);

        result.ShouldNotBeNull();
        result!.Name.ShouldBe("test-group");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotExists()
    {
        Group? result = await this._repository.GetByIdAsync(GroupId.Create());

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetByNameAsync_ReturnsGroup_WhenExists()
    {
        await this.SeedAsync("test-group", "Test Group");

        Group? result = await this._repository.GetByNameAsync("test-group");

        result.ShouldNotBeNull();
        result!.Name.ShouldBe("test-group");
    }

    [Fact]
    public async Task GetByNameAsync_ReturnsNull_WhenNotExists()
    {
        Group? result = await this._repository.GetByNameAsync("missing");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ExistsByNameAsync_ReturnsTrue_WhenExists()
    {
        await this.SeedAsync("test-group", "Test Group");

        bool exists = await this._repository.ExistsByNameAsync("test-group");

        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsByNameAsync_ReturnsFalse_WhenNotExists()
    {
        bool exists = await this._repository.ExistsByNameAsync("missing");

        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task ListAsync_ReturnsGroups_WithPagination()
    {
        await this.SeedAsync("group1", "Group 1");
        await this.SeedAsync("group2", "Group 2");
        await this.SeedAsync("group3", "Group 3");

        (IReadOnlyList<Group> items, int total) = await this._repository.ListAsync(1, 2, null);

        total.ShouldBe(3);
        items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ListAsync_FiltersBy_Search()
    {
        await this.SeedAsync("admin-group", "Admin Group");
        await this.SeedAsync("user-group", "User Group");

        (IReadOnlyList<Group> items, int total) = await this._repository.ListAsync(1, 10, "admin");

        total.ShouldBe(1);
        items.Count.ShouldBe(1);
        items[0].Name.ShouldBe("admin-group");
    }

    [Fact]
    public async Task AddAsync_AddsGroup()
    {
        Group group = Group.Create("new-group", "New Group", this._dateTimeProvider).Value;

        await this._repository.AddAsync(group);
        await this._repository.SaveChangesAsync();

        Group? saved = await this._dbContext.Groups.FirstOrDefaultAsync(g => g.Name == "new-group");
        saved.ShouldNotBeNull();
    }

    [Fact]
    public async Task Remove_RemovesGroup()
    {
        Group group = await this.SeedAsync("remove-group", "Remove Group");

        this._repository.Remove(group);
        await this._repository.SaveChangesAsync();

        Group? saved = await this._dbContext.Groups.FirstOrDefaultAsync(g => g.Id == group.Id);
        saved.ShouldBeNull();
    }

    [Fact]
    public async Task GetGroupsForUserAsync_ReturnsUserGroups()
    {
        Group group1 = await this.SeedAsync("group1", "Group 1");
        Group group2 = await this.SeedAsync("group2", "Group 2");
        
        UserId userId = UserId.Create();
        UserId assignedBy = UserId.Create();
        group1.AddMember(userId, assignedBy, this._dateTimeProvider);
        await this._repository.SaveChangesAsync();

        IReadOnlyList<Group> result = await this._repository.GetGroupsForUserAsync(userId);

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("group1");
    }

    [Fact]
    public async Task GetGroupsForUserAsync_ReturnsEmpty_WhenUserHasNoGroups()
    {
        await this.SeedAsync("group1", "Group 1");
        var userId = UserId.Create();

        IReadOnlyList<Group> result = await this._repository.GetGroupsForUserAsync(userId);

        result.Count.ShouldBe(0);
    }

    private Group CreateGroup(string name, string? description)
    {
        return Group.Create(name, description, this._dateTimeProvider).Value;
    }

    private async Task<Group> SeedAsync(string name, string? description)
    {
        Group group = this.CreateGroup(name, description);
        await this._repository.AddAsync(group);
        await this._repository.SaveChangesAsync();
        return group;
    }
}
