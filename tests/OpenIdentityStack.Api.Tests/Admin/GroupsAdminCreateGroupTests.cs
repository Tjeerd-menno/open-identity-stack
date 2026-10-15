using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenIdentityStack.Application;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Groups;
using OpenIdentityStack.Application.Groups.Commands;
using OpenIdentityStack.Domain.Groups;
using SharedKernel;

namespace OpenIdentityStack.Api.Tests.Admin;

public sealed class GroupsAdminCreateGroupTests
{
    [Fact]
    public async Task CreateAsync_ResolvesTheGroupsWorkflowAndCreatesAGroup()
    {
        IGroupRepository groups = Substitute.For<IGroupRepository>();
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        groups.ExistsByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        clock.UtcNow.Returns(new DateTimeOffset(2026, 1, 18, 12, 0, 0, TimeSpan.Zero));

        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(groups);
        services.AddSingleton(clock);
        services.AddSingleton(Substitute.For<IUserRepository>());
        services.AddSingleton(Substitute.For<IRoleRepository>());
        services.AddSingleton(Substitute.For<IPasswordHasher>());
        services.AddSingleton(Substitute.For<IPasswordPolicyValidator>());
        services.AddSingleton(Substitute.For<IAuditLog>());
        services.AddSingleton(Substitute.For<ICredentialLifecycleTransactionRunner>());
        services.AddSingleton(Substitute.For<ISessionRepository>());
        services.AddSingleton(Substitute.For<IUpstreamProviderRepository>());
        services.AddSingleton(Substitute.For<IAdministrativeApprovalAudit>());
        services.AddSingleton(Substitute.For<IAdministrativeAuthoritySnapshot>());
        services.AddSingleton(Substitute.For<IApplicationPermissionRegistryRepository>());
        services.AddApplication();

        await using ServiceProvider provider = services.BuildServiceProvider();
        IGroupsAdmin admin = provider.GetRequiredService<IGroupsAdmin>();

        Result<CreateGroupResponse> result = await admin.CreateAsync(new CreateGroupCommand("auditors", "Reviews access"));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("auditors");
        await groups.Received(1).AddAsync(
            Arg.Is<Group>(group => group.Name == "auditors"),
            Arg.Any<CancellationToken>());
    }
}
