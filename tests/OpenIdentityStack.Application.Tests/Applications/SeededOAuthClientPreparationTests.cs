using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Applications;
using OpenIdentityStack.Application.Resources;
using OpenIdentityStack.Domain.Applications;
using OpenIdentityStack.Domain.ApplicationPermissions;
using OpenIdentityStack.Domain.Resources;
using SharedKernel;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;

namespace OpenIdentityStack.Application.Tests.Applications;

public sealed class SeededOAuthClientPreparationTests
{
    private readonly IApplicationRepository applications = Substitute.For<IApplicationRepository>();
    private readonly IApplicationProtocolProjection projection = Substitute.For<IApplicationProtocolProjection>();
    private readonly IApplicationPermissionRegistryRepository registry = Substitute.For<IApplicationPermissionRegistryRepository>();
    private readonly IResourceAccessRepository resources = Substitute.For<IResourceAccessRepository>();
    private readonly IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();

    public SeededOAuthClientPreparationTests()
    {
        this.clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        this.projection.UpsertAsync(Arg.Any<DomainApplication>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
    }

    [Fact]
    public async Task PreparesDomainApplicationBeforeProjectingProtocolClient()
    {
        SeededOAuthClientPreparation preparation = this.CreatePreparation();
        var configuration = new SeededOAuthClientConfiguration(
            "oidf-code-client", "OIDF Code Client", ApplicationProfile.Web, OAuthClientType.Confidential,
            ["authorization_code", "refresh_token"], ["openid", "profile", "email"],
            ["https://certification.example/callback"], [], RequirePkce: false, RequireConsent: false);

        Result<DomainApplication> result = await preparation.PrepareAsync(configuration, "certification-secret");

        result.IsSuccess.ShouldBeTrue();
        await this.applications.Received(1).AddAsync(Arg.Is<DomainApplication>(client =>
            client.ClientId == configuration.ClientId && client.Status == ApplicationStatus.Active), Arg.Any<CancellationToken>());
        await this.applications.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await this.projection.Received(1).UpsertAsync(result.Value, "certification-secret", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PreparesTraceableResourceMappingsAndGrantsIdempotently()
    {
        SeededOAuthClientPreparation preparation = this.CreatePreparation();
        var configuration = new SeededOAuthClientConfiguration(
            "traceable-isotopes-web", "Traceable Isotopes Web", ApplicationProfile.SinglePage, OAuthClientType.Public,
            ["authorization_code", "refresh_token"], ["openid", "isotopes:read"],
            ["http://localhost:5176/callback"], [], RequirePkce: true, RequireConsent: false);
        var catalog = new SeededPermissionCatalogConfiguration(
            "traceable-isotopes", "Traceable Isotopes", "deployment-seed", OwnerType.User,
            [new("isotopes:read", "Read isotopes", null, "Isotopes")]);
        Result<DomainApplication> first = await preparation.PrepareAsync(configuration, null,
            [new("urn:traceable-isotopes:isotopes", "isotopes:read", "Traceable Isotopes read access",
                ["traceable-isotopes"], ["traceable-isotopes:isotopes:read"])], catalog);
        ProtectedResource resource = this.resources.ReceivedCalls()
            .Select(call => call.GetArguments().FirstOrDefault()).OfType<ProtectedResource>().Single();
        this.applications.GetByClientIdAsync(configuration.ClientId, Arg.Any<CancellationToken>()).Returns(first.Value);
        this.resources.FindByScopeAsync(resource.Scope, Arg.Any<CancellationToken>()).Returns(resource);
        ClientResourceGrant grant = this.resources.ReceivedCalls()
            .Select(call => call.GetArguments().FirstOrDefault()).OfType<ClientResourceGrant>().Single();
        this.resources.GetGrantAsync(first.Value.Id, resource.Id, Arg.Any<CancellationToken>()).Returns(grant);

        RegisteredApplication registeredCatalog = this.registry.ReceivedCalls()
            .Select(call => call.GetArguments().FirstOrDefault()).OfType<RegisteredApplication>().Single();
        this.registry.GetByIdentifierAsync(catalog.ApplicationIdentifier, Arg.Any<CancellationToken>()).Returns(registeredCatalog);

        Result<DomainApplication> second = await preparation.PrepareAsync(configuration, null,
            [new("urn:traceable-isotopes:isotopes", "isotopes:read", "Traceable Isotopes read access",
                ["traceable-isotopes"], ["traceable-isotopes:isotopes:read"])], catalog);

        second.IsSuccess.ShouldBeTrue();
        registeredCatalog.Permissions.Select(permission => permission.FullPermissionKey)
            .ShouldBe(["traceable-isotopes:isotopes:read"]);
        grant.DelegatedPermissions.ShouldBe(["traceable-isotopes:isotopes:read"]);
        await this.registry.Received(1).AddAsync(registeredCatalog, Arg.Any<CancellationToken>());
        await this.registry.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        this.resources.Received(1).AddResource(Arg.Any<ProtectedResource>());
        this.resources.Received(1).AddGrant(Arg.Any<ClientResourceGrant>());
    }

    [Fact]
    public async Task RejectsAWithdrawnOrDriftedControlledPermissionCatalog()
    {
        var catalogConfiguration = new SeededPermissionCatalogConfiguration(
            "traceable-isotopes", "Traceable Isotopes", "deployment-seed", OwnerType.User,
            [new("isotopes:read", "Read isotopes", null, "Isotopes")]);
        RegisteredApplication catalog = RegisteredApplication.Register(
            "traceable-isotopes", "Traceable Isotopes", null, "deployment-seed", OwnerType.User,
            [("isotopes:read", "Read isotopes", null, "Isotopes")], "deployment-seed", this.clock).Value;
        catalog.ChangeStatus(ApplicationLifecycleStatus.Disabled, false, "operator", this.clock).IsSuccess.ShouldBeTrue();
        this.registry.GetByIdentifierAsync("traceable-isotopes", Arg.Any<CancellationToken>()).Returns(catalog);
        var configuration = new SeededOAuthClientConfiguration(
            "traceable-isotopes-web", "Traceable Isotopes Web", ApplicationProfile.SinglePage, OAuthClientType.Public,
            ["authorization_code", "refresh_token"], ["openid", "isotopes:read"],
            ["http://localhost:5176/callback"], [], RequirePkce: true, RequireConsent: false);

        Result<DomainApplication> result = await this.CreatePreparation().PrepareAsync(
            configuration, null, [], catalogConfiguration);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Forbidden.Application.SeedIdentityMismatch");
        await this.projection.DidNotReceiveWithAnyArgs().UpsertAsync(default!, default(string?));
    }

    [Fact]
    public async Task PreparesResourceServerAuthorityWithoutProjectingAProtocolClient()
    {
        var configuration = new SeededOAuthClientConfiguration(
            "isotopes-api-resource", "Isotopes API Resource Server", ApplicationProfile.MachineToMachine,
            OAuthClientType.Confidential, ["client_credentials"], ["isotopes:read", "exports:read"],
            [], [], RequirePkce: false, RequireConsent: false);
        SeededProtectedResourceConfiguration[] protectedResources =
        [
            new("urn:traceable-isotopes:scope:isotopes:read", "isotopes:read", "Read isotopes", ["traceable-isotopes"], []),
            new("urn:traceable-isotopes:scope:exports:read", "exports:read", "Read exports", ["traceable-isotopes"], [])
        ];

        Result<DomainApplication> result = await this.CreatePreparation()
            .PrepareAuthorityOnlyAsync(configuration, protectedResources);

        result.IsSuccess.ShouldBeTrue();
        await this.projection.DidNotReceiveWithAnyArgs().UpsertAsync(default!, default(string?));
        this.resources.Received(2).AddGrant(Arg.Is<ClientResourceGrant>(grant =>
            grant.ClientApplicationId == result.Value.Id
            && grant.DelegatedPermissions.Count == 0
            && grant.ApplicationPermissions.Count == 0));
    }

    private SeededOAuthClientPreparation CreatePreparation() =>
        new(this.applications, this.projection, this.resources, this.registry, this.clock);
}
