using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenIdentityStack.Application;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Users;
using OpenIdentityStack.Application.Users.Commands;
using OpenIdentityStack.Domain.Users;
using SharedKernel;

namespace OpenIdentityStack.Api.Tests.Admin;

public sealed class UsersAdminCreateUserTests
{
    [Fact]
    public async Task CreateAsync_ResolvesTheUsersWorkflowAndCreatesAUser()
    {
        IUserRepository users = Substitute.For<IUserRepository>();
        IPasswordHasher passwordHasher = Substitute.For<IPasswordHasher>();
        IPasswordPolicyValidator passwordPolicy = Substitute.For<IPasswordPolicyValidator>();
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        passwordHasher.HashPassword(Arg.Any<string>()).Returns(call => $"hashed_{call.Arg<string>()}");
        passwordPolicy.ValidatePassword(Arg.Any<string>()).Returns(Result.Success());
        clock.UtcNow.Returns(new DateTimeOffset(2026, 1, 18, 12, 0, 0, TimeSpan.Zero));
        users.ExistsByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(users);
        services.AddSingleton(passwordHasher);
        services.AddSingleton(passwordPolicy);
        services.AddSingleton(clock);
        services.AddSingleton(Substitute.For<IAuditLog>());
        services.AddSingleton(Substitute.For<ICredentialLifecycleTransactionRunner>());
        services.AddSingleton(Substitute.For<ISessionRepository>());
        services.AddSingleton(Substitute.For<IRoleRepository>());
        services.AddSingleton(Substitute.For<IGroupRepository>());
        services.AddSingleton(Substitute.For<IUpstreamProviderRepository>());
        services.AddSingleton(Substitute.For<IAdministrativeApprovalAudit>());
        services.AddSingleton(Substitute.For<IAdministrativeAuthoritySnapshot>());
        services.AddApplication();

        await using ServiceProvider provider = services.BuildServiceProvider();
        IUsersAdmin admin = provider.GetRequiredService<IUsersAdmin>();

        Result<CreateUserResult> result = await admin.CreateAsync(
            new CreateUserCommand("ada@example.com", "Ada Lovelace", "Password123!", "admin-1"));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Email.ShouldBe("ada@example.com");
        await users.Received(1).AddAsync(
            Arg.Is<User>(user => user.Email == "ada@example.com"),
            Arg.Any<CancellationToken>());
    }
}
