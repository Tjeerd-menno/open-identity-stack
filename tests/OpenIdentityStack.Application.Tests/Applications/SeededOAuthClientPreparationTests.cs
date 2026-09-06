using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Applications;
using OpenIdentityStack.Application.Resources;
using OpenIdentityStack.Domain.Applications;
using OpenIdentityStack.Domain.Resources;
using SharedKernel;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;

namespace OpenIdentityStack.Application.Tests.Applications;

public sealed class SeededOAuthClientPreparationTests
{
    private readonly IApplicationRepository applications = Substitute.For<IApplicationRepository>();
    private readonly IApplicationProtocolProjection projection = Substitute.For<IApplicationProtocolProjection>();
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
        var preparation = new SeededOAuthClientPreparation(this.applications, this.projection, this.resources, this.clock);
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
        var preparation = new SeededOAuthClientPreparation(this.applications, this.projection, this.resources, this.clock);
        var configuration = new SeededOAuthClientConfiguration(
            "traceable-isotopes-web", "Traceable Isotopes Web", ApplicationProfile.SinglePage, OAuthClientType.Public,
            ["authorization_code", "refresh_token"], ["openid", "isotopes:read"],
            ["http://localhost:5176/callback"], [], RequirePkce: true, RequireConsent: false);
        Result<DomainApplication> first = await preparation.PrepareAsync(configuration, null,
            [new("urn:traceable-isotopes:isotopes", "isotopes:read", "Traceable Isotopes read access", ["traceable-isotopes"], ["*"])]);
        ProtectedResource resource = this.resources.ReceivedCalls()
            .Select(call => call.GetArguments().FirstOrDefault()).OfType<ProtectedResource>().Single();
        this.applications.GetByClientIdAsync(configuration.ClientId, Arg.Any<CancellationToken>()).Returns(first.Value);
        this.resources.FindByScopeAsync(resource.Scope, Arg.Any<CancellationToken>()).Returns(resource);
        ClientResourceGrant grant = this.resources.ReceivedCalls()
            .Select(call => call.GetArguments().FirstOrDefault()).OfType<ClientResourceGrant>().Single();
        this.resources.GetGrantAsync(first.Value.Id, resource.Id, Arg.Any<CancellationToken>()).Returns(grant);

        Result<DomainApplication> second = await preparation.PrepareAsync(configuration, null,
            [new("urn:traceable-isotopes:isotopes", "isotopes:read", "Traceable Isotopes read access", ["traceable-isotopes"], ["*"])]);

        second.IsSuccess.ShouldBeTrue();
        this.resources.Received(1).AddResource(Arg.Any<ProtectedResource>());
        this.resources.Received(1).AddGrant(Arg.Any<ClientResourceGrant>());
    }
}
