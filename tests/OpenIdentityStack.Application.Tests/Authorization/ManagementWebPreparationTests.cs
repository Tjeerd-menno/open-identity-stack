using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.AdministrativeAccess;
using OpenIdentityStack.Application.Applications;
using OpenIdentityStack.Application.Resources;
using OpenIdentityStack.Domain.Applications;
using OpenIdentityStack.Domain.Resources;
using SharedKernel;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;

namespace OpenIdentityStack.Application.Tests.Authorization;

public sealed class ManagementWebPreparationTests
{
    private readonly IApplicationRepository applications = Substitute.For<IApplicationRepository>();
    private readonly IApplicationProtocolProjection projection = Substitute.For<IApplicationProtocolProjection>();
    private readonly IResourceAccessRepository resources = Substitute.For<IResourceAccessRepository>();
    private readonly IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
    private readonly ManagementWebPreparation preparation;
    private static readonly string[] redirects = ["https://admin.example.com/auth/callback"];

    public ManagementWebPreparationTests()
    {
        this.clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        this.projection.UpsertAsync(Arg.Any<DomainApplication>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        this.preparation = new(this.applications, this.projection, this.resources, this.clock);
    }

    [Fact]
    public async Task PreparationDoesNotApproveByDefault()
    {
        (await this.preparation.PrepareAsync(redirects, [], false)).IsSuccess.ShouldBeTrue();
        await this.applications.Received(1).AddAsync(Arg.Is<DomainApplication>(client => client.ClientId == "management-web-client" && client.AllowedScopes.Contains("ois.admin")), Arg.Any<CancellationToken>());
        this.resources.DidNotReceive().AddGrant(Arg.Any<ClientResourceGrant>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EveryPreparationRejectsUnreviewedConfigurationWithoutProjectionOrRepair(bool bootstrap)
    {
        DomainApplication client = this.ExistingClient(["https://unreviewed.example/callback"]);
        this.applications.GetByClientIdAsync("management-web-client", Arg.Any<CancellationToken>()).Returns(client);
        ClientResourceGrant withdrawn = ClientResourceGrant.Create(client.Id, ProtectedResource.AdministrativeResourceId, [], []).Value;
        this.resources.GetGrantAsync(client.Id, ProtectedResource.AdministrativeResourceId, Arg.Any<CancellationToken>()).Returns(withdrawn);

        (await this.preparation.PrepareAsync(redirects, [], bootstrap)).IsFailure.ShouldBeTrue();

        client.RedirectUris.ShouldBe(["https://unreviewed.example/callback"]);
        withdrawn.DelegatedPermissions.ShouldBeEmpty();
        await this.projection.DidNotReceiveWithAnyArgs().UpsertAsync(default!, default);
        await this.applications.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BootstrapDoesNotSilentlyRestoreRemovedScopes()
    {
        DomainApplication client = this.ExistingClient(redirects);
        client.ConfigureOAuth(ApplicationProfile.SinglePage, OAuthClientType.Public, ["authorization_code", "refresh_token"],
            ["openid", "profile", "email"], redirects, [], true, false, this.clock).IsSuccess.ShouldBeTrue();
        this.applications.GetByClientIdAsync("management-web-client", Arg.Any<CancellationToken>()).Returns(client);

        (await this.preparation.PrepareAsync(redirects, [], true)).IsFailure.ShouldBeTrue();

        client.AllowedScopes.ShouldNotContain("ois.admin");
        await this.projection.DidNotReceiveWithAnyArgs().UpsertAsync(default!, default);
        this.resources.DidNotReceive().AddGrant(Arg.Any<ClientResourceGrant>());
    }

    [Fact]
    public async Task ExplicitBootstrapCreatesOnlyKnownManagementDelegatedGrant()
    {
        (await this.preparation.PrepareAsync(redirects, [], true)).IsSuccess.ShouldBeTrue();
        this.resources.Received(1).AddGrant(Arg.Is<ClientResourceGrant>(grant => grant.ResourceId == ProtectedResource.AdministrativeResourceId
            && grant.DelegatedPermissions.Count == 1 && grant.DelegatedPermissions[0] == "*" && grant.ApplicationPermissions.Count == 0));
    }

    [Fact]
    public async Task RerunPreservesWithdrawnGrant()
    {
        DomainApplication client = this.ExistingClient(redirects);
        this.applications.GetByClientIdAsync("management-web-client", Arg.Any<CancellationToken>()).Returns(client);
        ClientResourceGrant withdrawn = ClientResourceGrant.Create(client.Id, ProtectedResource.AdministrativeResourceId, [], []).Value;
        this.resources.GetGrantAsync(client.Id, ProtectedResource.AdministrativeResourceId, Arg.Any<CancellationToken>()).Returns(withdrawn);
        (await this.preparation.PrepareAsync(redirects, [], true)).IsSuccess.ShouldBeTrue();
        withdrawn.DelegatedPermissions.ShouldBeEmpty();
        withdrawn.Revision.ShouldBe(1);
        this.resources.DidNotReceive().AddGrant(Arg.Any<ClientResourceGrant>());
    }

    [Fact]
    public async Task BootstrapRejectsReplacedRedirectsOrDisabledManagementClient()
    {
        DomainApplication client = this.ExistingClient(["https://attacker.example/callback"]);
        this.applications.GetByClientIdAsync("management-web-client", Arg.Any<CancellationToken>()).Returns(client);
        (await this.preparation.PrepareAsync(redirects, [], true)).IsFailure.ShouldBeTrue();
        client.Disable(this.clock);
        (await this.preparation.PrepareAsync(["https://attacker.example/callback"], [], true)).IsFailure.ShouldBeTrue();
        this.resources.DidNotReceive().AddGrant(Arg.Any<ClientResourceGrant>());
    }

    private DomainApplication ExistingClient(IReadOnlyList<string> redirectUris) => DomainApplication.Create("management-web-client", "Management", null,
        ApplicationProfile.SinglePage, OAuthClientType.Public, ["authorization_code", "refresh_token"],
        ["openid", "profile", "email", "ois.admin"], redirectUris, [], true, false, this.clock).Value;
}
