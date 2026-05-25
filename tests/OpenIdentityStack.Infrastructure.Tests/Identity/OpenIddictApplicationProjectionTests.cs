using Microsoft.Extensions.Logging;
using OpenIdentityStack.Domain.Applications;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Infrastructure.Identity;
using OpenIddict.Abstractions;
using SharedKernel;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;

#pragma warning disable IDE0007 // Use var instead of explicit type
#pragma warning disable IDE0008 // Use explicit type instead of var
#pragma warning disable CA2012 // NSubstitute setup captures ValueTask-returning calls.

namespace OpenIdentityStack.Infrastructure.Tests.Identity;

public sealed class OpenIddictApplicationProjectionTests
{
    private readonly IOpenIddictApplicationManager applicationManager;
    private readonly IOpenIddictScopeManager scopeManager;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly OpenIddictApplicationProjection projection;
    private readonly DateTimeOffset now = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);

    public OpenIddictApplicationProjectionTests()
    {
        this.applicationManager = Substitute.For<IOpenIddictApplicationManager>();
        this.scopeManager = Substitute.For<IOpenIddictScopeManager>();
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.dateTimeProvider.UtcNow.Returns(this.now);
        this.projection = new OpenIddictApplicationProjection(
            this.applicationManager,
            this.scopeManager,
            Substitute.For<ILogger<OpenIddictApplicationProjection>>());
    }

    [Fact]
    public async Task UpsertAsync_WhenApplicationIsActiveAndMissing_CreatesOpenIddictApplication()
    {
        DomainApplication application = this.CreateWebApplication();
        this.applicationManager.FindByClientIdAsync(application.ClientId, Arg.Any<CancellationToken>())
            .Returns((object?)null);
        this.applicationManager.CreateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<object>(new object()));

        Result result = await this.projection.UpsertAsync(application);

        result.IsSuccess.ShouldBeTrue();
        await this.applicationManager.Received(1).CreateAsync(
            Arg.Is<OpenIddictApplicationDescriptor>(descriptor =>
                descriptor.ClientId == application.ClientId &&
                descriptor.DisplayName == application.DisplayName &&
                descriptor.ClientType == OpenIddictConstants.ClientTypes.Confidential &&
                !string.IsNullOrEmpty(descriptor.ClientSecret) &&
                descriptor.ConsentType == OpenIddictConstants.ConsentTypes.Explicit &&
                descriptor.RedirectUris.Contains(new Uri("https://orders.example.com/callback")) &&
                descriptor.Permissions.Contains(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode) &&
                descriptor.Permissions.Contains(OpenIddictConstants.Permissions.ResponseTypes.Code) &&
                descriptor.Permissions.Contains(OpenIddictConstants.Permissions.Prefixes.Scope + "orders.read") &&
                descriptor.Requirements.Contains(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpsertAsync_WithExplicitClientSecret_UsesProvidedSecret()
    {
        DomainApplication application = this.CreateWebApplication();
        this.applicationManager.FindByClientIdAsync(application.ClientId, Arg.Any<CancellationToken>())
            .Returns((object?)null);
        this.applicationManager.CreateAsync(Arg.Any<OpenIddictApplicationDescriptor>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<object>(new object()));

        Result result = await this.projection.UpsertAsync(application, "legacy-secret");

        result.IsSuccess.ShouldBeTrue();
        await this.applicationManager.Received(1).CreateAsync(
            Arg.Is<OpenIddictApplicationDescriptor>(descriptor => descriptor.ClientSecret == "legacy-secret"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpsertAsync_WhenApplicationIsActiveAndExists_UpdatesOpenIddictApplication()
    {
        DomainApplication application = this.CreateWebApplication();
        object existing = new();
        this.applicationManager.FindByClientIdAsync(application.ClientId, Arg.Any<CancellationToken>())
            .Returns(existing);

        Result result = await this.projection.UpsertAsync(application);

        result.IsSuccess.ShouldBeTrue();
        await this.applicationManager.Received(1).PopulateAsync(
            Arg.Any<OpenIddictApplicationDescriptor>(),
            existing,
            Arg.Any<CancellationToken>());
        await this.applicationManager.Received(1).UpdateAsync(
            existing,
            Arg.Is<OpenIddictApplicationDescriptor>(descriptor =>
                descriptor.ClientId == application.ClientId &&
                descriptor.DisplayName == application.DisplayName),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpsertAsync_WhenApplicationIsDisabled_DeletesExistingOpenIddictApplication()
    {
        DomainApplication application = this.CreateWebApplication();
        application.Disable(this.dateTimeProvider).IsSuccess.ShouldBeTrue();
        object existing = new();
        this.applicationManager.FindByClientIdAsync(application.ClientId, Arg.Any<CancellationToken>())
            .Returns(existing);

        Result result = await this.projection.UpsertAsync(application);

        result.IsSuccess.ShouldBeTrue();
        await this.applicationManager.Received(1).DeleteAsync(existing, Arg.Any<CancellationToken>());
        await this.applicationManager.DidNotReceive().CreateAsync(
            Arg.Any<OpenIddictApplicationDescriptor>(),
            Arg.Any<CancellationToken>());
    }

    private DomainApplication CreateWebApplication() =>
        DomainApplication.Create(
            "orders-web",
            "Orders Web",
            "Orders web app",
            ApplicationProfile.Web,
            OAuthClientType.Confidential,
            ["authorization_code", "refresh_token"],
            ["openid", "orders.read"],
            ["https://orders.example.com/callback"],
            ["https://orders.example.com/logout"],
            requirePkce: true,
            requireConsent: true,
            this.dateTimeProvider).Value;
}
