using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Tests.Common;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence;

public sealed class LocalUserBootstrapperTests(SqliteTestFixture fixture) : IClassFixture<SqliteTestFixture>
{
    [Theory]
    [InlineData(UserStatus.Disabled, true)]
    [InlineData(UserStatus.PendingVerification, true)]
    [InlineData(UserStatus.Active, true)]
    [InlineData(UserStatus.Disabled, false)]
    public async Task CreateIfAbsentAsync_ExistingEmailNeverAuthorizesSecurityChanges(UserStatus status, bool administrator)
    {
        await fixture.ClearAllDataAsync();
        await using OpenIdentityStackDbContext db = fixture.CreateDbContext();
        await SeedData.SeedAsync(db);
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        User existing = status == UserStatus.PendingVerification
            ? User.CreateLocal("existing@example.com", "Original", "original-password-hash", clock).Value
            : User.CreateBootstrap("existing@example.com", "Original", "original-password-hash", clock).Value;
        if (status == UserStatus.Disabled)
        {
            existing.Disable("Administrative decision", clock).IsSuccess.ShouldBeTrue();
        }

        db.Users.Add(existing);
        await db.SaveChangesAsync();
        var bootstrapper = new LocalUserBootstrapper(db, Substitute.For<IPasswordHasher>(), Substitute.For<IPasswordPolicyValidator>(), clock);

        for (int run = 0; run < 2; run++)
        {
            (await bootstrapper.CreateIfAbsentAsync("EXISTING@example.com", "Replacement", "NewPassword123!", administrator)).ShouldBeFalse();
        }

        await using OpenIdentityStackDbContext verification = fixture.CreateDbContext();
        User persisted = await verification.Users.SingleAsync();
        persisted.Status.ShouldBe(status);
        persisted.PasswordHash.ShouldBe("original-password-hash");
        persisted.DisplayName.ShouldBe("Original");
        (await verification.RoleAssignments.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task CreateIfAbsentAsync_ChangingConfiguredEmailCannotBypassExistingDisablement()
    {
        await fixture.ClearAllDataAsync();
        await using OpenIdentityStackDbContext db = fixture.CreateDbContext();
        await SeedData.SeedAsync(db);
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        User existing = User.CreateBootstrap("original@example.com", "Original", "original-hash", clock).Value;
        existing.Disable("Administrative decision", clock).IsSuccess.ShouldBeTrue();
        db.Users.Add(existing);
        await db.SaveChangesAsync();
        IPasswordHasher hasher = Substitute.For<IPasswordHasher>();
        hasher.HashPassword(Arg.Any<string>()).Returns("new-hash");
        IPasswordPolicyValidator validator = Substitute.For<IPasswordPolicyValidator>();
        validator.ValidatePassword(Arg.Any<string>()).Returns(Result.Success());
        var bootstrapper = new LocalUserBootstrapper(db, hasher, validator, clock);

        bool created = await bootstrapper.CreateIfAbsentAsync("replacement@example.com", "Replacement", "NewPassword123!", assignAdministrator: true);

        created.ShouldBeFalse();
        (await db.Users.CountAsync()).ShouldBe(1);
        (await db.RoleAssignments.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task CreateIfAbsentAsync_CreatesActiveAdministratorAndDurableAudit()
    {
        await fixture.ClearAllDataAsync();
        await using OpenIdentityStackDbContext db = fixture.CreateDbContext();
        await SeedData.SeedAsync(db);
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        IPasswordHasher hasher = Substitute.For<IPasswordHasher>();
        hasher.HashPassword("SecretPassword123!").Returns("hashed-secret");
        IPasswordPolicyValidator validator = Substitute.For<IPasswordPolicyValidator>();
        validator.ValidatePassword(Arg.Any<string>()).Returns(Result.Success());
        var bootstrapper = new LocalUserBootstrapper(db, hasher, validator, clock);

        bool created = await bootstrapper.CreateIfAbsentAsync("bootstrap@example.com", "Bootstrap", "SecretPassword123!", assignAdministrator: true);

        created.ShouldBeTrue();
        await using OpenIdentityStackDbContext verification = fixture.CreateDbContext();
        User user = await verification.Users.SingleAsync();
        user.Status.ShouldBe(UserStatus.Active);
        user.PasswordHash.ShouldBe("hashed-secret");
        (await verification.RoleAssignments.CountAsync(assignment => assignment.UserId == user.Id)).ShouldBe(1);
        (await verification.AuditLogEntries.CountAsync(entry => entry.Action == "User.BootstrapCreated")).ShouldBe(1);
    }
}
