using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Applications;
using OpenIdentityStack.Application.Resources;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.ApplicationPermissions;
using OpenIdentityStack.Domain.Applications;
using OpenIdentityStack.Domain.Resources;
using OpenIdentityStack.Domain.Users;
using SharedKernel;
using ApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;
using ClientApplication = OpenIdentityStack.Domain.Applications.Application;

namespace OpenIdentityStack.Application.Tests.Authorization;

public sealed class ResourcePermissionServiceTests
{
    private readonly IResourceAccessRepository resources = Substitute.For<IResourceAccessRepository>();
    private readonly IApplicationRepository applications = Substitute.For<IApplicationRepository>();
    private readonly IApplicationPermissionRegistryRepository registry = Substitute.For<IApplicationPermissionRegistryRepository>();
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IGetUserEffectiveRolesQueryHandler roles = Substitute.For<IGetUserEffectiveRolesQueryHandler>();
    private readonly IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
    private readonly ProtectedResource resource;
    private readonly ClientApplication client;
    private readonly User user;
    private readonly ResourcePermissionService service;
    private readonly Dictionary<string, ProtectedResource> scopes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ProtectedResource> audiences = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, ClientResourceGrant> grants = [];

    public ResourcePermissionServiceTests()
    {
        this.clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        this.resource = ProtectedResource.Create("https://orders.example.com", "orders-api", "Orders", ["orders"]).Value;
        this.client = ClientApplication.CreateMachineToMachine("unrelated-client-name", "Client", null, ["orders-api"], this.clock).Value;
        this.user = User.CreateFederated("user@example.com", "User", this.clock).Value;
        RegisteredApplication catalog = RegisteredApplication.Register("orders", "Orders", null, this.user.Id.Value.ToString(), OwnerType.User,
            [("invoice:read", "Read", null, null), ("invoice:write", "Write", null, null)], this.user.Id.Value.ToString(), this.clock).Value;
        this.scopes[this.resource.Scope] = this.resource;
        this.audiences[this.resource.Audience] = this.resource;
        this.resources.FindByScopesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(call => (IReadOnlyList<ProtectedResource>)call.Arg<IEnumerable<string>>().Where(this.scopes.ContainsKey).Select(scope => this.scopes[scope]).ToArray());
        this.resources.FindByAudiencesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(call => (IReadOnlyList<ProtectedResource>)call.Arg<IEnumerable<string>>().Where(this.audiences.ContainsKey).Select(audience => this.audiences[audience]).ToArray());
        this.resources.ListGrantsAsync(Arg.Any<ApplicationId>(), Arg.Any<CancellationToken>())
            .Returns(call => (IReadOnlyList<ClientResourceGrant>)this.grants.Values.Where(grant => grant.ClientApplicationId == call.Arg<ApplicationId>()).ToArray());
        this.applications.GetTokenProjectionAsync(this.client.ClientId, Arg.Any<CancellationToken>()).Returns(ClientProjection(this.client));
        this.registry.GetByIdentifierAsync("orders", Arg.Any<CancellationToken>()).Returns(catalog);
        this.users.GetByIdAsync(this.user.Id, Arg.Any<CancellationToken>()).Returns(this.user);
        this.roles.HandleAsync(this.user.Id, Arg.Any<CancellationToken>()).Returns((Result<IReadOnlyList<RoleDto>>)new[]
        {
            new RoleDto(Guid.NewGuid(), "operator", "Operator", null, false, true, ["orders:invoice:*", "payroll:salary:read", "*"])
        });
        this.grants[this.resource.Id] = ClientResourceGrant.Create(this.client.Id, this.resource.Id, ["orders:invoice:read"], ["orders:invoice:write"]).Value;
        this.service = new ResourcePermissionService(this.resources, this.applications, this.registry, this.users, this.roles);
    }

    [Theory]
    [InlineData(true, "orders:invoice:read")]
    [InlineData(false, "orders:invoice:write")]
    public async Task ProjectAsync_IntersectsExplicitResourceAndSubjectSpecificAssignment(bool delegated, string expected)
    {
        Result<ResourceTokenProjection> result = await this.service.ProjectAsync(new ResourceTokenRequest(
            this.client.ClientId, ["orders-api"], [], delegated ? this.user.Id : null));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Permissions.ShouldBe([expected]);
        result.Value.Audiences.ShouldBe(["https://orders.example.com"]);
        result.Value.GrantRevisions[this.resource.Id].ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ProjectAsync_ReusesPrecomputedUserPermissionsWithoutResolvingRoles()
    {
        Result<ResourceTokenProjection> result = await this.service.ProjectAsync(new ResourceTokenRequest(
            this.client.ClientId, ["orders-api"], [], this.user.Id, UserPermissions: ["orders:invoice:read"]));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Permissions.ShouldBe(["orders:invoice:read"]);
        await this.roles.DidNotReceive().HandleAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>());

        Result<ResourceTokenProjection> denied = await this.service.ProjectAsync(new ResourceTokenRequest(
            this.client.ClientId, ["orders-api"], [], this.user.Id, UserPermissions: []));
        denied.IsSuccess.ShouldBeTrue();
        denied.Value.Permissions.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProjectAsync_RefreshNeverAddsNewAuthorityAndReflectsReducedGrant()
    {
        Result<ResourceTokenProjection> result = await this.service.ProjectAsync(new ResourceTokenRequest(
            this.client.ClientId, ["orders-api"], [], null, ["orders:invoice:read"], [this.resource.Audience]));
        result.IsSuccess.ShouldBeTrue();
        result.Value.Permissions.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProjectAsync_RejectsAdministrativeProjectionWhenUserRolesAreFullyRevoked()
    {
        var administrativeResource = ProtectedResource.CreateAdministrative();
        ClientApplication administrativeClient = ClientApplication.CreateMachineToMachine(
            "admin-client", "Admin client", null, [ProtectedResource.AdministrativeScope], this.clock).Value;
        this.applications.GetTokenProjectionAsync(administrativeClient.ClientId, Arg.Any<CancellationToken>()).Returns(ClientProjection(administrativeClient));
        this.scopes[ProtectedResource.AdministrativeScope] = administrativeResource;
        this.audiences[ProtectedResource.AdministrativeAudience] = administrativeResource;
        this.grants[administrativeResource.Id] = ClientResourceGrant.Create(administrativeClient.Id, administrativeResource.Id, ["users:read"], []).Value;
        this.roles.HandleAsync(this.user.Id, Arg.Any<CancellationToken>())
            .Returns((Result<IReadOnlyList<RoleDto>>)Array.Empty<RoleDto>());

        Result<ResourceTokenProjection> result = await this.service.ProjectAsync(new ResourceTokenRequest(
            administrativeClient.ClientId, [ProtectedResource.AdministrativeScope],
            [ProtectedResource.AdministrativeAudience], this.user.Id));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ResourceAccessErrors.NotGranted);
        await this.roles.Received(1).HandleAsync(this.user.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProjectAsync_RejectsMissingDisabledAndUnscopedClientsFromTheTokenProjection()
    {
        this.applications.GetTokenProjectionAsync(this.client.ClientId, Arg.Any<CancellationToken>())
            .Returns((ClientTokenProjection?)null);
        Result<ResourceTokenProjection> missing = await this.service.ProjectAsync(new ResourceTokenRequest(
            this.client.ClientId, ["orders-api"], [], null));
        missing.Error.ShouldBe(ResourceAccessErrors.NotGranted);

        this.applications.GetTokenProjectionAsync(this.client.ClientId, Arg.Any<CancellationToken>())
            .Returns(new ClientTokenProjection(this.client.Id, ApplicationStatus.Disabled, this.client.AllowedScopes));
        Result<ResourceTokenProjection> disabled = await this.service.ProjectAsync(new ResourceTokenRequest(
            this.client.ClientId, ["orders-api"], [], null));
        disabled.Error.ShouldBe(ResourceAccessErrors.NotGranted);

        this.applications.GetTokenProjectionAsync(this.client.ClientId, Arg.Any<CancellationToken>())
            .Returns(new ClientTokenProjection(this.client.Id, ApplicationStatus.Active, []));
        Result<ResourceTokenProjection> unscoped = await this.service.ProjectAsync(new ResourceTokenRequest(
            this.client.ClientId, ["orders-api"], [], null));
        unscoped.Error.ShouldBe(ResourceAccessErrors.NotGranted);
        await this.applications.DidNotReceive().GetByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("unknown", "")]
    [InlineData("orders-api", "https://payroll.example.com")]
    public async Task ProjectAsync_RejectsUnknownOrMismatchedResources(string scope, string audience)
    {
        Result<ResourceTokenProjection> result = await this.service.ProjectAsync(new ResourceTokenRequest(
            this.client.ClientId, [scope], audience.Length == 0 ? [] : [audience], null));
        result.Error.ShouldBe(ResourceAccessErrors.UnknownResource);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProjectAsync_EmptySubjectCeilingAndMissingGrantAreDenied(bool delegated)
    {
        this.grants[this.resource.Id] = ClientResourceGrant.Create(this.client.Id, this.resource.Id,
            delegated ? [] : ["orders:invoice:read"],
            delegated ? ["orders:invoice:write"] : []).Value;
        Result<ResourceTokenProjection> empty = await this.service.ProjectAsync(new ResourceTokenRequest(
            this.client.ClientId, ["orders-api"], [], delegated ? this.user.Id : null));
        empty.Error.ShouldBe(ResourceAccessErrors.NotGranted);
        this.grants.Remove(this.resource.Id);
        Result<ResourceTokenProjection> absent = await this.service.ProjectAsync(new ResourceTokenRequest(
            this.client.ClientId, ["orders-api"], [], delegated ? this.user.Id : null));
        absent.Error.ShouldBe(ResourceAccessErrors.NotGranted);
    }

    [Fact]
    public async Task ProjectAsync_RejectsCombinedAdministrativeAndBusinessResource()
    {
        this.scopes[ProtectedResource.AdministrativeScope] = ProtectedResource.CreateAdministrative();
        Result<ResourceTokenProjection> result = await this.service.ProjectAsync(new ResourceTokenRequest(
            this.client.ClientId, ["orders-api", ProtectedResource.AdministrativeScope], [], null));
        result.Error.ShouldBe(ResourceAccessErrors.UnknownResource);
    }

    [Fact]
    public async Task SaveGrantAsync_CannotUseOrdinaryApplicationEditingToGrantAdminAccess()
    {
        var admin = ProtectedResource.CreateAdministrative();
        this.applications.GetByIdAsync(this.client.Id, Arg.Any<CancellationToken>()).Returns(this.client);
        this.resources.GetResourceAsync(admin.Id, Arg.Any<CancellationToken>()).Returns(admin);
        var workflow = new ResourceAccessWorkflow(this.resources, this.applications, this.registry);
        Result<ClientResourceGrantDto> result = await workflow.SaveGrantAsync(this.client.Id.Value, admin.Id,
            new ClientResourceGrantConfiguration(["*"], ["*"]), "actor");
        result.Error.ShouldBe(ResourceAccessErrors.Reserved);
        await this.resources.DidNotReceiveWithAnyArgs().SaveChangesAsync(default!, default!, default!);
    }

    [Fact]
    public async Task ProjectAsync_BatchesScopeAudienceAndGrantLookups()
    {
        ProtectedResource payroll = ProtectedResource.Create("https://payroll.example.com", "payroll-api", "Payroll", ["payroll"]).Value;
        ClientApplication batchClient = ClientApplication.CreateMachineToMachine(
            "batch-client", "Batch", null, ["orders-api", "payroll-api"], this.clock).Value;
        this.applications.GetTokenProjectionAsync(batchClient.ClientId, Arg.Any<CancellationToken>()).Returns(ClientProjection(batchClient));
        RegisteredApplication payrollCatalog = RegisteredApplication.Register("payroll", "Payroll", null, this.user.Id.Value.ToString(), OwnerType.User,
            [("salary:read", "Read", null, null)], this.user.Id.Value.ToString(), this.clock).Value;
        this.registry.GetByIdentifierAsync("payroll", Arg.Any<CancellationToken>()).Returns(payrollCatalog);
        this.scopes[payroll.Scope] = payroll;
        this.audiences[payroll.Audience] = payroll;
        this.grants[this.resource.Id] = ClientResourceGrant.Create(batchClient.Id, this.resource.Id, [], ["orders:invoice:write"]).Value;
        this.grants[payroll.Id] = ClientResourceGrant.Create(batchClient.Id, payroll.Id, [], ["payroll:salary:read"]).Value;

        Result<ResourceTokenProjection> result = await this.service.ProjectAsync(new ResourceTokenRequest(
            batchClient.ClientId, ["openid", "payroll-api", "orders-api", "profile"], [payroll.Audience, this.resource.Audience], null));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Permissions.ShouldBe(["orders:invoice:write", "payroll:salary:read"]);
        result.Value.Audiences.ShouldBe(["https://orders.example.com", "https://payroll.example.com"]);
        result.Value.GrantRevisions.Count.ShouldBe(2);
        result.Value.GrantRevisions[this.resource.Id].ShouldBeGreaterThan(0);
        result.Value.GrantRevisions[payroll.Id].ShouldBeGreaterThan(0);
        string[] scopes = ["payroll-api", "orders-api"];
        await this.resources.Received(1).FindByScopesAsync(
            Arg.Is<IEnumerable<string>>(requested => requested.SequenceEqual(scopes)),
            Arg.Any<CancellationToken>());
        await this.resources.Received(1).FindByAudiencesAsync(
            Arg.Is<IEnumerable<string>>(requested => requested.SequenceEqual(new[] { payroll.Audience, this.resource.Audience })),
            Arg.Any<CancellationToken>());
        await this.resources.Received(1).ListGrantsAsync(batchClient.Id, Arg.Any<CancellationToken>());
        await this.resources.DidNotReceive().FindByScopeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await this.resources.DidNotReceive().FindByAudienceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await this.resources.DidNotReceive().GetGrantAsync(Arg.Any<ApplicationId>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await this.applications.DidNotReceive().GetByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProjectAsync_RejectsDisabledResourceBeforeLoadingGrants()
    {
        this.resource.Configure("Orders", ["orders"], false).IsSuccess.ShouldBeTrue();

        Result<ResourceTokenProjection> result = await this.service.ProjectAsync(new ResourceTokenRequest(
            this.client.ClientId, ["openid", "orders-api"], [], null));

        result.Error.ShouldBe(ResourceAccessErrors.UnknownResource);
        string[] scopes = ["orders-api"];
        await this.resources.Received(1).FindByScopesAsync(
            Arg.Is<IEnumerable<string>>(requested => requested.SequenceEqual(scopes)),
            Arg.Any<CancellationToken>());
        await this.resources.DidNotReceive().FindByAudiencesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
        await this.resources.DidNotReceive().ListGrantsAsync(Arg.Any<ApplicationId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProjectAsync_IgnoresProtocolScopesWithoutAResourceLookup()
    {
        Result<ResourceTokenProjection> result = await this.service.ProjectAsync(new ResourceTokenRequest(
            this.client.ClientId, ["openid", "profile"], [], null));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Audiences.ShouldBeEmpty();
        result.Value.Permissions.ShouldBeEmpty();
        result.Value.GrantRevisions.ShouldBeEmpty();
        await this.resources.DidNotReceive().FindByScopesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
        await this.resources.DidNotReceive().FindByAudiencesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
        await this.resources.DidNotReceive().ListGrantsAsync(Arg.Any<ApplicationId>(), Arg.Any<CancellationToken>());
    }

    private static ClientTokenProjection ClientProjection(ClientApplication application) =>
        new(application.Id, application.Status, application.AllowedScopes);
}
