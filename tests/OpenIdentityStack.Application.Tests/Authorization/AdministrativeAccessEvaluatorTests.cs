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
        EvaluatorFixture fixture = CreateEvaluator([delegated], [machine]);
        Result<IReadOnlyList<string>> result = await fixture.Evaluator.EvaluateAsync(new("approved-client", human ? fixture.User.Id : null, ["users:read", "roles:read"]));
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe([expected]);
    }

    [Fact]
    public async Task CurrentCeilingAndOriginalTokenBoundAccess()
    {
        EvaluatorFixture fixture = CreateEvaluator(["*"], []);
        (await fixture.Evaluator.EvaluateAsync(new("approved-client", fixture.User.Id, ["users:read"]))).Value.ShouldBe(["users:read"]);
        fixture.Grant.Configure(["roles:read"], []).IsSuccess.ShouldBeTrue();
        fixture.Authority.Current++;
        (await fixture.SecondEvaluator.EvaluateAsync(new("approved-client", fixture.User.Id, ["users:read"]))).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task WithdrawingEntitlementDeniesIssuedMachineToken()
    {
        EvaluatorFixture fixture = CreateEvaluator([], ["users:read"]);
        (await fixture.Evaluator.EvaluateAsync(new("approved-client", null, ["users:read"]))).IsSuccess.ShouldBeTrue();
        fixture.Grant.Configure([], []).IsSuccess.ShouldBeTrue();
        fixture.Authority.Current++;
        (await fixture.SecondEvaluator.EvaluateAsync(new("approved-client", null, ["users:read"]))).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task UnapprovedClientDoesNotBorrowHumanPermissions()
    {
        EvaluatorFixture fixture = CreateEvaluator(["*"], []);
        fixture.Resources.ListGrantsAsync(Arg.Any<OpenIdentityStack.Domain.Applications.ApplicationId>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<ClientResourceGrant>());
        (await fixture.Evaluator.EvaluateAsync(new("approved-client", fixture.User.Id, ["users:read"]))).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task RepeatedEvaluationAtTheSameAuthorityRevisionDoesNotResolveRolesOrTheGrantAgain()
    {
        EvaluatorFixture fixture = CreateEvaluator(["users:read", "roles:read"], []);
        var first = new AdministrativeAccessRequest("approved-client", fixture.User.Id, ["users:read", "roles:read"]);
        var reversedCeiling = new AdministrativeAccessRequest("approved-client", fixture.User.Id, ["roles:read", "users:read"]);

        (await fixture.Evaluator.EvaluateAsync(first)).Value.ShouldBe(["roles:read", "users:read"]);
        (await fixture.SecondEvaluator.EvaluateAsync(reversedCeiling)).Value.ShouldBe(["roles:read", "users:read"]);

        await fixture.Roles.Received(1).HandleAsync(fixture.User.Id, Arg.Any<CancellationToken>());
        await fixture.Resources.Received(1).ListGrantsAsync(fixture.Grant.ClientApplicationId, Arg.Any<CancellationToken>());
        await fixture.Clients.Received(1).GetTokenProjectionAsync("approved-client", Arg.Any<CancellationToken>());
        await fixture.Users.Received(1).GetByIdAsync(fixture.User.Id, Arg.Any<CancellationToken>());
        await fixture.Resources.Received(1).FindByAudiencesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
        await fixture.Resources.Received(2).FindByScopesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
        await fixture.Resources.DidNotReceive().GetGrantAsync(Arg.Any<OpenIdentityStack.Domain.Applications.ApplicationId>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await fixture.Resources.DidNotReceive().FindByScopeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuthorityRevisionChangeReevaluatesAndObservesWithdrawal()
    {
        EvaluatorFixture fixture = CreateEvaluator(["*"], []);
        var request = new AdministrativeAccessRequest("approved-client", fixture.User.Id, ["users:read", "roles:read"]);
        (await fixture.Evaluator.EvaluateAsync(request)).Value.ShouldBe(["roles:read", "users:read"]);

        fixture.Grant.Configure(["roles:read"], []).IsSuccess.ShouldBeTrue();
        fixture.Authority.Current++;

        (await fixture.SecondEvaluator.EvaluateAsync(request)).Value.ShouldBe(["roles:read"]);
        await fixture.Roles.Received(2).HandleAsync(fixture.User.Id, Arg.Any<CancellationToken>());
        await fixture.Resources.Received(2).ListGrantsAsync(fixture.Grant.ClientApplicationId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TokenPermissionCeilingIsNotReplacedByAnotherCachedDecision()
    {
        EvaluatorFixture fixture = CreateEvaluator(["*"], []);
        (await fixture.Evaluator.EvaluateAsync(new("approved-client", fixture.User.Id, ["users:read"]))).Value.ShouldBe(["users:read"]);
        (await fixture.SecondEvaluator.EvaluateAsync(new("approved-client", fixture.User.Id, ["users:read", "roles:read"]))).Value
            .ShouldBe(["roles:read", "users:read"]);
    }

    [Fact]
    public async Task HumanAndMachineDecisionsDoNotShareACachedProjection()
    {
        EvaluatorFixture fixture = CreateEvaluator(["users:read"], ["roles:read"]);
        (await fixture.Evaluator.EvaluateAsync(new("approved-client", null, ["users:read", "roles:read"]))).Value.ShouldBe(["roles:read"]);
        (await fixture.SecondEvaluator.EvaluateAsync(new("approved-client", fixture.User.Id, ["users:read", "roles:read"]))).Value
            .ShouldBe(["users:read"]);
        await fixture.Roles.Received(1).HandleAsync(fixture.User.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MissingAdministrativeResourceDoesNotReturnCachedDecision()
    {
        EvaluatorFixture fixture = CreateEvaluator(["*"], []);
        var request = new AdministrativeAccessRequest("approved-client", fixture.User.Id, ["users:read"]);
        (await fixture.Evaluator.EvaluateAsync(request)).Value.ShouldBe(["users:read"]);
        fixture.Resources.FindByScopesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ProtectedResource>());

        (await fixture.SecondEvaluator.EvaluateAsync(request)).IsFailure.ShouldBeTrue();
        await fixture.Clients.Received(2).GetTokenProjectionAsync("approved-client", Arg.Any<CancellationToken>());
        await fixture.Roles.Received(1).HandleAsync(fixture.User.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnreadableAdministrativeResourceDoesNotReturnCachedDecision()
    {
        EvaluatorFixture fixture = CreateEvaluator(["*"], []);
        var request = new AdministrativeAccessRequest("approved-client", fixture.User.Id, ["users:read"]);
        int reads = 0;
        fixture.Resources.FindByScopesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (++reads > 1)
                {
                    throw new InvalidOperationException("ProtectedResources unavailable");
                }

                return (IReadOnlyList<ProtectedResource>)new[] { ProtectedResource.CreateAdministrative() };
            });

        (await fixture.Evaluator.EvaluateAsync(request)).Value.ShouldBe(["users:read"]);
        InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(() => fixture.SecondEvaluator.EvaluateAsync(request));
        failure.Message.ShouldBe("ProtectedResources unavailable");
        await fixture.Roles.Received(1).HandleAsync(fixture.User.Id, Arg.Any<CancellationToken>());
    }

    private static EvaluatorFixture CreateEvaluator(IReadOnlyList<string> delegated, IReadOnlyList<string> machine)
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        DomainApplication client = DomainApplication.Create("approved-client", "Approved", null, ApplicationProfile.MachineToMachine,
            OAuthClientType.Confidential, ["client_credentials"], ["ois.admin"], [], [], false, false, clock).Value;
        User user = User.CreateFederated("operator@example.com", "Operator", clock).Value;
        IApplicationRepository clients = Substitute.For<IApplicationRepository>();
        clients.GetTokenProjectionAsync("approved-client", Arg.Any<CancellationToken>())
            .Returns(new ClientTokenProjection(client.Id, client.Status, client.AllowedScopes));
        IUserRepository users = Substitute.For<IUserRepository>();
        users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        IGetUserEffectiveRolesQueryHandler roles = Substitute.For<IGetUserEffectiveRolesQueryHandler>();
        IReadOnlyList<RoleDto> userRoles = [new(Guid.NewGuid(), "operator", "Operator", null, false, true, ["*"])];
        roles.HandleAsync(user.Id, Arg.Any<CancellationToken>()).Returns((Result<IReadOnlyList<RoleDto>>)userRoles.ToList());
        IResourceAccessRepository resources = Substitute.For<IResourceAccessRepository>();
        var admin = ProtectedResource.CreateAdministrative();
        resources.FindByScopesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>()).Returns(new[] { admin });
        resources.FindByAudiencesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>()).Returns(new[] { admin });
        ClientResourceGrant grant = ClientResourceGrant.Create(client.Id, admin.Id, delegated, machine).Value;
        resources.ListGrantsAsync(client.Id, Arg.Any<CancellationToken>()).Returns(new[] { grant });
        var cache = new AdministrativeAccessDecisionCache();
        var authority = new MutableAuthorityRevision();
        IApplicationPermissionRegistryRepository registry = Substitute.For<IApplicationPermissionRegistryRepository>();
        var first = new ResourcePermissionService(resources, clients, registry, users, roles, cache);
        var second = new ResourcePermissionService(resources, clients, registry, users, roles, cache);
        return new EvaluatorFixture(
            new AdministrativeAccessEvaluator(first, authority),
            new AdministrativeAccessEvaluator(second, authority),
            grant,
            resources,
            clients,
            users,
            user,
            roles,
            authority);
    }

    private sealed class MutableAuthorityRevision : IAdministrativeAuthoritySnapshot
    {
        public long Current { get; set; }

        public Task<long> CaptureAsync(CancellationToken cancellationToken = default) => Task.FromResult(this.Current);
    }

    private sealed record EvaluatorFixture(
        AdministrativeAccessEvaluator Evaluator,
        AdministrativeAccessEvaluator SecondEvaluator,
        ClientResourceGrant Grant,
        IResourceAccessRepository Resources,
        IApplicationRepository Clients,
        IUserRepository Users,
        User User,
        IGetUserEffectiveRolesQueryHandler Roles,
        MutableAuthorityRevision Authority);
}
