using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Applications;
using OpenIdentityStack.Application.Resources;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.ApplicationPermissions;
using OpenIdentityStack.Domain.Resources;
using OpenIdentityStack.Domain.Users;
using SharedKernel;
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

    public ResourcePermissionServiceTests()
    {
        this.clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        this.resource = ProtectedResource.Create("https://orders.example.com", "orders-api", "Orders", ["orders"]).Value;
        this.client = ClientApplication.CreateMachineToMachine("unrelated-client-name", "Client", null, ["orders-api"], this.clock).Value;
        this.user = User.CreateFederated("user@example.com", "User", this.clock).Value;
        RegisteredApplication catalog = RegisteredApplication.Register("orders", "Orders", null, this.user.Id.Value.ToString(), OwnerType.User,
            [("invoice:read", "Read", null, null), ("invoice:write", "Write", null, null)], this.user.Id.Value.ToString(), this.clock).Value;
        this.resources.FindByScopeAsync("orders-api", Arg.Any<CancellationToken>()).Returns(this.resource);
        this.resources.FindByAudienceAsync(this.resource.Audience, Arg.Any<CancellationToken>()).Returns(this.resource);
        this.applications.GetByClientIdAsync(this.client.ClientId, Arg.Any<CancellationToken>()).Returns(this.client);
        this.registry.GetByIdentifierAsync("orders", Arg.Any<CancellationToken>()).Returns(catalog);
        this.users.GetByIdAsync(this.user.Id, Arg.Any<CancellationToken>()).Returns(this.user);
        this.roles.HandleAsync(this.user.Id, Arg.Any<CancellationToken>()).Returns((Result<IReadOnlyList<RoleDto>>)new[]
        {
            new RoleDto(Guid.NewGuid(), "operator", "Operator", null, false, true, ["orders:invoice:*", "payroll:salary:read", "*"])
        });
        this.resources.GetGrantAsync(this.client.Id, this.resource.Id, Arg.Any<CancellationToken>()).Returns(
            ClientResourceGrant.Create(this.client.Id, this.resource.Id, ["orders:invoice:read"], ["orders:invoice:write"]).Value);
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
    public async Task ProjectAsync_RefreshNeverAddsNewAuthorityAndReflectsReducedGrant()
    {
        Result<ResourceTokenProjection> result = await this.service.ProjectAsync(new ResourceTokenRequest(
            this.client.ClientId, ["orders-api"], [], null, ["orders:invoice:read"], [this.resource.Audience]));
        result.IsSuccess.ShouldBeTrue();
        result.Value.Permissions.ShouldBeEmpty();
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
        this.resources.GetGrantAsync(this.client.Id, this.resource.Id, Arg.Any<CancellationToken>()).Returns(
            ClientResourceGrant.Create(this.client.Id, this.resource.Id,
                delegated ? [] : ["orders:invoice:read"],
                delegated ? ["orders:invoice:write"] : []).Value);
        Result<ResourceTokenProjection> empty = await this.service.ProjectAsync(new ResourceTokenRequest(
            this.client.ClientId, ["orders-api"], [], delegated ? this.user.Id : null));
        empty.Error.ShouldBe(ResourceAccessErrors.NotGranted);
        this.resources.GetGrantAsync(this.client.Id, this.resource.Id, Arg.Any<CancellationToken>()).Returns((ClientResourceGrant?)null);
        Result<ResourceTokenProjection> absent = await this.service.ProjectAsync(new ResourceTokenRequest(
            this.client.ClientId, ["orders-api"], [], delegated ? this.user.Id : null));
        absent.Error.ShouldBe(ResourceAccessErrors.NotGranted);
    }

    [Fact]
    public async Task ProjectAsync_RejectsCombinedAdministrativeAndBusinessResource()
    {
        this.resources.FindByScopeAsync(ProtectedResource.AdministrativeScope, Arg.Any<CancellationToken>()).Returns(ProtectedResource.CreateAdministrative());
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
}
