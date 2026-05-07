using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Groups;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Domain.ServiceAccounts;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Tests.Common;

using SharedKernel;
#pragma warning disable IDE0007 // Use var instead of explicit type
#pragma warning disable IDE0008 // Use explicit type instead of var

namespace OpenIdentityStack.Infrastructure.Tests.Persistence;

public sealed class OpenIdentityStackDbContextTests : IClassFixture<SqliteTestFixture>, IAsyncLifetime
{
    private readonly SqliteTestFixture _fixture;
    private OpenIdentityStackDbContext _context = null!;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly DateTimeOffset _now = new(2026, 1, 22, 12, 0, 0, TimeSpan.Zero);

    public OpenIdentityStackDbContextTests(SqliteTestFixture fixture)
    {
        this._fixture = fixture;
        IDateTimeProvider provider = Substitute.For<IDateTimeProvider>();
        provider.UtcNow.Returns(this._now);
        this._dateTimeProvider = provider;
    }

    public async ValueTask InitializeAsync()
    {
        await this._fixture.ClearAllDataAsync();
        this._context = this._fixture.CreateDbContext();
    }

    public async ValueTask DisposeAsync()
    {
        await this._context.DisposeAsync();
    }

    [Fact]
    public void Users_DbSet_ShouldBeAccessible()
    {
        DbSet<User> users = this._context.Users;
        users.ShouldNotBeNull();
    }

    [Fact]
    public void ServiceAccounts_DbSet_ShouldBeAccessible()
    {
        DbSet<ServiceAccount> serviceAccounts = this._context.ServiceAccounts;
        serviceAccounts.ShouldNotBeNull();
    }

    [Fact]
    public void Roles_DbSet_ShouldBeAccessible()
    {
        DbSet<Role> roles = this._context.Roles;
        roles.ShouldNotBeNull();
    }

    [Fact]
    public void Groups_DbSet_ShouldBeAccessible()
    {
        DbSet<Group> groups = this._context.Groups;
        groups.ShouldNotBeNull();
    }

    [Fact]
    public void UserSessions_DbSet_ShouldBeAccessible()
    {
        DbSet<UserSession> sessions = this._context.UserSessions;
        sessions.ShouldNotBeNull();
    }

    [Fact]
    public void UpstreamProviders_DbSet_ShouldBeAccessible()
    {
        DbSet<UpstreamProvider> providers = this._context.UpstreamProviders;
        providers.ShouldNotBeNull();
    }

    [Fact]
    public async Task CanSaveAndRetrieve_User()
    {
        User user = User.CreateLocal("test@example.com", "Test User", "hash", this._dateTimeProvider).Value;

        this._context.Users.Add(user);
        await this._context.SaveChangesAsync();

        User? retrieved = await this._context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        retrieved.ShouldNotBeNull();
        retrieved!.Email.ShouldBe("test@example.com");
    }

    [Fact]
    public async Task CanSaveAndRetrieve_Role()
    {
        Role role = Role.Create("test-role", "Test Role").Value;

        this._context.Roles.Add(role);
        await this._context.SaveChangesAsync();

        Role? retrieved = await this._context.Roles.FirstOrDefaultAsync(r => r.Id == role.Id);
        retrieved.ShouldNotBeNull();
        retrieved!.Name.ShouldBe("test-role");
    }

    [Fact]
    public async Task CanSaveAndRetrieve_Group()
    {
        Group group = Group.Create("test-group", "Test Group", this._dateTimeProvider).Value;

        this._context.Groups.Add(group);
        await this._context.SaveChangesAsync();

        Group? retrieved = await this._context.Groups.FirstOrDefaultAsync(g => g.Id == group.Id);
        retrieved.ShouldNotBeNull();
        retrieved!.Name.ShouldBe("test-group");
    }

    [Fact]
    public async Task CanSaveAndRetrieve_ServiceAccount()
    {
        ServiceAccount account = ServiceAccount.Create(
            "test-client",
            "Test Client",
            new List<string> { "api" },
            new List<string> { "client_credentials" },
            this._dateTimeProvider).Value;

        this._context.ServiceAccounts.Add(account);
        await this._context.SaveChangesAsync();

        ServiceAccount? retrieved = await this._context.ServiceAccounts.FirstOrDefaultAsync(s => s.Id == account.Id);
        retrieved.ShouldNotBeNull();
        retrieved!.ClientId.ShouldBe("test-client");
    }

    [Fact]
    public async Task CanSaveAndRetrieve_UserSession()
    {
        UserSession session = UserSession.Create(
            UserId.Create(),
            "127.0.0.1",
            "Test Browser",
            this._dateTimeProvider).Value;

        this._context.UserSessions.Add(session);
        await this._context.SaveChangesAsync();

        UserSession? retrieved = await this._context.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
        retrieved.ShouldNotBeNull();
        retrieved!.IpAddress.ShouldBe("127.0.0.1");
    }

    [Fact]
    public async Task CanSaveAndRetrieve_UpstreamProvider()
    {
        UpstreamProvider provider = UpstreamProvider.Create(
            "test-provider",
            "Test Provider",
            "https://example.com",
            "client-id").Value;

        this._context.UpstreamProviders.Add(provider);
        await this._context.SaveChangesAsync();

        UpstreamProvider? retrieved = await this._context.UpstreamProviders.FirstOrDefaultAsync(p => p.Id == provider.Id);
        retrieved.ShouldNotBeNull();
        retrieved!.Name.ShouldBe("test-provider");
    }

    [Fact]
    public async Task StronglyTypedIds_ArePersisted_Correctly()
    {
        UserId userId = UserId.Create();
        User user = User.CreateLocal("test@example.com", "Test", "hash", this._dateTimeProvider).Value;
        
        this._context.Users.Add(user);
        await this._context.SaveChangesAsync();
        
        this._context.Entry(user).State = EntityState.Detached;
        
        User? retrieved = await this._context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        retrieved.ShouldNotBeNull();
        retrieved!.Id.Value.ShouldBe(user.Id.Value);
    }

    [Fact]
    public async Task RoleAssignments_CanBeSaved()
    {
        Role role = Role.Create("test-role", "Test").Value;
        UserId userId = UserId.Create();
        RoleAssignment assignment = RoleAssignment.Create(userId, role.Id, this._now).Value;

        this._context.Roles.Add(role);
        this._context.RoleAssignments.Add(assignment);
        await this._context.SaveChangesAsync();

        RoleAssignment? retrieved = await this._context.RoleAssignments
            .FirstOrDefaultAsync(ra => ra.UserId == userId && ra.RoleId == role.Id);
        retrieved.ShouldNotBeNull();
    }
}
