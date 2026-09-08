using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.AdministrativeAccess;
using OpenIdentityStack.Application.Applications;
using OpenIdentityStack.Application.Resources;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Applications;
using OpenIdentityStack.Domain.Resources;
using OpenIdentityStack.Domain.Users;
using SharedKernel;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;

namespace OpenIdentityStack.Application.Tests.Authorization;

public sealed class AdministrativeAccessEvaluatorTests
{
    [Theory]
    [InlineData(true, "users:read", "roles:read", "users:read")]
    [InlineData(false, "users:read", "roles:read", "roles:read")]
    public async Task DelegatedAndMachinePermissionsUseSeparateApprovedCeilings(bool human, string delegated, string machine, string expected)
    {
        (AdministrativeAccessEvaluator evaluator, _, _, User user) = CreateEvaluator([delegated], [machine]);
        Result<IReadOnlyList<string>> result = await evaluator.EvaluateAsync(new("approved-client", human ? user.Id : null, ["users:read", "roles:read"]));
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe([expected]);
    }

    [Fact]
    public async Task CurrentCeilingAndOriginalTokenBoundAccess()
    {
        (AdministrativeAccessEvaluator evaluator, ClientResourceGrant grant, _, User user) = CreateEvaluator(["*"], []);
        (await evaluator.EvaluateAsync(new("approved-client", user.Id, ["users:read"]))).Value.ShouldBe(["users:read"]);
        grant.Configure(["roles:read"], []);
        (await evaluator.EvaluateAsync(new("approved-client", user.Id, ["users:read"]))).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task WithdrawingEntitlementDeniesIssuedMachineToken()
    {
        (AdministrativeAccessEvaluator evaluator, ClientResourceGrant grant, _, _) = CreateEvaluator([], ["users:read"]);
        (await evaluator.EvaluateAsync(new("approved-client", null, ["users:read"]))).IsSuccess.ShouldBeTrue();
        grant.Configure([], []);
        (await evaluator.EvaluateAsync(new("approved-client", null, ["users:read"]))).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task UnapprovedClientDoesNotBorrowHumanPermissions()
    {
        (AdministrativeAccessEvaluator evaluator, _, IResourceAccessRepository resources, User user) = CreateEvaluator(["*"], []);
        resources.GetGrantAsync(Arg.Any<OpenIdentityStack.Domain.Applications.ApplicationId>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ClientResourceGrant?)null);
        (await evaluator.EvaluateAsync(new("approved-client", user.Id, ["users:read"]))).IsFailure.ShouldBeTrue();
    }

    private static (AdministrativeAccessEvaluator, ClientResourceGrant, IResourceAccessRepository, User) CreateEvaluator(IReadOnlyList<string> delegated, IReadOnlyList<string> machine)
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        DomainApplication client = DomainApplication.Create("approved-client", "Approved", null, ApplicationProfile.MachineToMachine,
            OAuthClientType.Confidential, ["client_credentials"], ["ois.admin"], [], [], false, false, clock).Value;
        User user = User.CreateFederated("operator@example.com", "Operator", clock).Value;
        IApplicationRepository clients = Substitute.For<IApplicationRepository>();
        clients.GetByClientIdAsync("approved-client", Arg.Any<CancellationToken>()).Returns(client);
        IUserRepository users = Substitute.For<IUserRepository>();
        users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        IGetUserEffectiveRolesQueryHandler roles = Substitute.For<IGetUserEffectiveRolesQueryHandler>();
        IReadOnlyList<RoleDto> userRoles = [new(Guid.NewGuid(), "operator", "Operator", null, false, true, ["*"])];
        roles.HandleAsync(user.Id, Arg.Any<CancellationToken>()).Returns((Result<IReadOnlyList<RoleDto>>)userRoles.ToList());
        IResourceAccessRepository resources = Substitute.For<IResourceAccessRepository>();
        var admin = ProtectedResource.CreateAdministrative();
        resources.FindByScopeAsync("ois.admin", Arg.Any<CancellationToken>()).Returns(admin);
        resources.FindByAudienceAsync(ProtectedResource.AdministrativeAudience, Arg.Any<CancellationToken>()).Returns(admin);
        ClientResourceGrant grant = ClientResourceGrant.Create(client.Id, admin.Id, delegated, machine).Value;
        resources.GetGrantAsync(client.Id, admin.Id, Arg.Any<CancellationToken>()).Returns(grant);
        var projection = new ResourcePermissionService(resources, clients, Substitute.For<IApplicationPermissionRegistryRepository>(), users, roles);
        return (new AdministrativeAccessEvaluator(projection, Substitute.For<IAdministrativeAuthoritySnapshot>()), grant, resources, user);
    }
}
