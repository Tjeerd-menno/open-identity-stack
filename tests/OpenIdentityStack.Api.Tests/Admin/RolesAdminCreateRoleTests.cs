using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenIdentityStack.Application;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Roles;
using OpenIdentityStack.Application.Roles.Commands;
using OpenIdentityStack.Domain.Roles;
using SharedKernel;

namespace OpenIdentityStack.Api.Tests.Admin;

public sealed class RolesAdminCreateRoleTests
{
    [Fact]
    public async Task CreateAsync_ResolvesTheRolesWorkflowAndCreatesARole()
    {
        IRoleRepository roles = Substitute.For<IRoleRepository>();
        roles.ExistsByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(roles);
        services.AddSingleton(Substitute.For<IUserRepository>());
        services.AddSingleton(Substitute.For<IPasswordHasher>());
        services.AddSingleton(Substitute.For<IPasswordPolicyValidator>());
        services.AddSingleton(Substitute.For<IDateTimeProvider>());
        services.AddSingleton(Substitute.For<IAuditLog>());
        services.AddSingleton(Substitute.For<ICredentialLifecycleTransactionRunner>());
        services.AddSingleton(Substitute.For<ISessionRepository>());
        services.AddSingleton(Substitute.For<IGroupRepository>());
        services.AddSingleton(Substitute.For<IUpstreamProviderRepository>());
        services.AddSingleton(Substitute.For<IAdministrativeApprovalAudit>());
        services.AddSingleton(Substitute.For<IAdministrativeAuthoritySnapshot>());
        services.AddSingleton(Substitute.For<IApplicationPermissionRegistryRepository>());
        services.AddApplication();

        await using ServiceProvider provider = services.BuildServiceProvider();
        IRolesAdmin admin = provider.GetRequiredService<IRolesAdmin>();

        Result<CreateRoleResponse> result = await admin.CreateAsync(
            new CreateRoleCommand("auditors", "Auditors", "Reviews access", null));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("auditors");
        await roles.Received(1).AddAsync(
            Arg.Is<Role>(role => role.Name == "auditors"),
            Arg.Any<CancellationToken>());
    }
}
