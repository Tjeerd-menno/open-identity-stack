using OpenIdentityStack.Domain.Applications;
using SharedKernel;

namespace OpenIdentityStack.Domain.Tests.Applications;

public sealed class ApplicationOAuthConfigurationTests
{
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly DateTimeOffset now = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);

    public ApplicationOAuthConfigurationTests()
    {
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.dateTimeProvider.UtcNow.Returns(this.now);
    }

    [Fact]
    public void Create_MachineToMachineWithClientCredentials_ReturnsConfidentialApplicationWithoutInteractiveUris()
    {
        Result<Application> result = this.Create(
            ApplicationType.MachineToMachine,
            OAuthClientType.Confidential,
            ["client_credentials"],
            ["api.read"],
            [],
            [],
            requirePkce: false,
            requireConsent: false);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Type.ShouldBe(ApplicationType.MachineToMachine);
        result.Value.ClientType.ShouldBe(OAuthClientType.Confidential);
        result.Value.AllowedGrantTypes.ShouldBe(["client_credentials"]);
        result.Value.RedirectUris.ShouldBeEmpty();
        result.Value.RequireConsent.ShouldBeFalse();
    }

    [Fact]
    public void Create_MachineToMachineWithPublicClient_ReturnsInvalidClientType()
    {
        Result<Application> result = this.Create(
            ApplicationType.MachineToMachine,
            OAuthClientType.Public,
            ["client_credentials"],
            ["api.read"],
            [],
            [],
            requirePkce: false,
            requireConsent: false);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ApplicationErrors.InvalidClientType.Code);
    }

    [Fact]
    public void Create_MachineToMachineWithRedirectUri_ReturnsRedirectUrisNotAllowed()
    {
        Result<Application> result = this.Create(
            ApplicationType.MachineToMachine,
            OAuthClientType.Confidential,
            ["client_credentials"],
            ["api.read"],
            ["https://app.example.com/callback"],
            [],
            requirePkce: false,
            requireConsent: false);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ApplicationErrors.RedirectUrisNotAllowed.Code);
    }

    [Fact]
    public void Create_AuthorizationCodeWithoutRedirectUri_ReturnsRedirectUriRequired()
    {
        Result<Application> result = this.Create(
            ApplicationType.Web,
            OAuthClientType.Confidential,
            ["authorization_code"],
            ["openid"],
            [],
            [],
            requirePkce: false,
            requireConsent: true);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ApplicationErrors.RedirectUriRequired.Code);
    }

    [Fact]
    public void Create_PublicAuthorizationCodeWithoutPkce_ReturnsPkceRequired()
    {
        Result<Application> result = this.Create(
            ApplicationType.SinglePage,
            OAuthClientType.Public,
            ["authorization_code"],
            ["openid"],
            ["https://spa.example.com/callback"],
            [],
            requirePkce: false,
            requireConsent: true);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ApplicationErrors.PkceRequired.Code);
    }

    [Fact]
    public void ConfigureOAuth_WithValidValues_UpdatesConfigurationAndRaisesEvent()
    {
        Application application = this.Create(
            ApplicationType.Web,
            OAuthClientType.Confidential,
            ["authorization_code"],
            ["openid"],
            ["https://web.example.com/callback"],
            [],
            requirePkce: false,
            requireConsent: true).Value;

        Result result = application.ConfigureOAuth(
            ApplicationType.Web,
            OAuthClientType.Confidential,
            ["authorization_code", "refresh_token"],
            ["openid", "profile"],
            ["https://web.example.com/callback", "https://web.example.com/signin-oidc"],
            ["https://web.example.com/signout-callback-oidc"],
            requirePkce: true,
            requireConsent: true,
            this.dateTimeProvider);

        result.IsSuccess.ShouldBeTrue();
        application.Type.ShouldBe(ApplicationType.Web);
        application.ClientType.ShouldBe(OAuthClientType.Confidential);
        application.RequirePkce.ShouldBeTrue();
        application.AllowedScopes.ShouldBe(["openid", "profile"]);
        application.AllowedGrantTypes.ShouldBe(["authorization_code", "refresh_token"]);
        application.RedirectUris.ShouldBe(["https://web.example.com/callback", "https://web.example.com/signin-oidc"]);
        application.PostLogoutRedirectUris.ShouldBe(["https://web.example.com/signout-callback-oidc"]);
        application.GetDomainEvents().ShouldContain(e => e is ApplicationDomainEvents.ApplicationOAuthConfigured);
    }

    private Result<Application> Create(
        ApplicationType type,
        OAuthClientType clientType,
        IReadOnlyList<string> allowedGrantTypes,
        IReadOnlyList<string> allowedScopes,
        IReadOnlyList<string> redirectUris,
        IReadOnlyList<string> postLogoutRedirectUris,
        bool requirePkce,
        bool requireConsent) =>
        Application.Create(
            "orders-app",
            "Orders App",
            null,
            type,
            clientType,
            allowedGrantTypes,
            allowedScopes,
            redirectUris,
            postLogoutRedirectUris,
            requirePkce,
            requireConsent,
            this.dateTimeProvider);
}
