using OpenIdentityStack.Domain.Applications;
using SharedKernel;

namespace OpenIdentityStack.Domain.Tests.Applications;

public sealed class ApplicationTypePolicyTests
{
    private readonly IDateTimeProvider dateTimeProvider;

    public ApplicationTypePolicyTests()
    {
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2026, 5, 24, 12, 0, 0, TimeSpan.Zero));
    }

    [Theory]
    [InlineData(ApplicationType.Web, ClientProfile.Confidential, "authorization_code", true, true, true)]
    [InlineData(ApplicationType.SinglePage, ClientProfile.Public, "authorization_code", true, true, true)]
    [InlineData(ApplicationType.Native, ClientProfile.Public, "authorization_code", true, true, true)]
    [InlineData(ApplicationType.MachineToMachine, ClientProfile.Confidential, "client_credentials", false, false, true)]
    public void GetPolicy_ForSupportedType_ReturnsExpectedDefaults(
        ApplicationType type,
        ClientProfile expectedProfile,
        string expectedDefaultGrant,
        bool expectedRedirects,
        bool expectedConsent,
        bool expectedSelectable)
    {
        ApplicationTypePolicy policy = ApplicationTypePolicyCatalog.GetPolicy(type);

        policy.ApplicationType.ShouldBe(type);
        policy.DefaultClientProfile.ShouldBe(expectedProfile);
        policy.DefaultGrantTypes.ShouldContain(expectedDefaultGrant);
        policy.RequiresRedirectUris.ShouldBe(expectedRedirects);
        policy.DefaultRequireConsent.ShouldBe(expectedConsent);
        policy.IsSelectable.ShouldBe(expectedSelectable);
    }

    [Fact]
    public void GetPolicy_Web_ExposesExpectedOptionAvailability()
    {
        ApplicationTypePolicy policy = ApplicationTypePolicyCatalog.GetPolicy(ApplicationType.Web);

        policy.Options[ApplicationOptionKey.ClientSecrets].ShouldBe(ApplicationOptionAvailability.Available);
        policy.Options[ApplicationOptionKey.AuthorizationCodeGrant].ShouldBe(ApplicationOptionAvailability.Available);
        policy.Options[ApplicationOptionKey.ClientCredentialsGrant].ShouldBe(ApplicationOptionAvailability.Hidden);
        policy.Options[ApplicationOptionKey.Pkce].ShouldBe(ApplicationOptionAvailability.Available);
        policy.Options[ApplicationOptionKey.PostLogoutRedirectUris].ShouldBe(ApplicationOptionAvailability.Available);
    }

    [Fact]
    public void GetPolicy_SinglePage_HidesCredentialsAndRequiresPkce()
    {
        ApplicationTypePolicy policy = ApplicationTypePolicyCatalog.GetPolicy(ApplicationType.SinglePage);

        policy.Options[ApplicationOptionKey.ClientProfile].ShouldBe(ApplicationOptionAvailability.ReadOnly);
        policy.Options[ApplicationOptionKey.ClientSecrets].ShouldBe(ApplicationOptionAvailability.Hidden);
        policy.Options[ApplicationOptionKey.ClientCertificates].ShouldBe(ApplicationOptionAvailability.Hidden);
        policy.Options[ApplicationOptionKey.Pkce].ShouldBe(ApplicationOptionAvailability.ReadOnly);
        policy.RequirePkce.ShouldBeTrue();
    }

    [Fact]
    public void GetPolicy_Native_SupportsNativeRedirectPatterns()
    {
        ApplicationTypePolicy policy = ApplicationTypePolicyCatalog.GetPolicy(ApplicationType.Native);

        policy.Options[ApplicationOptionKey.NativeRedirectUriPatterns].ShouldBe(ApplicationOptionAvailability.Available);
        policy.Options[ApplicationOptionKey.LoopbackRedirectUri].ShouldBe(ApplicationOptionAvailability.Available);
        policy.Options[ApplicationOptionKey.ClientSecrets].ShouldBe(ApplicationOptionAvailability.Hidden);
        policy.RequirePkce.ShouldBeTrue();
    }

    [Fact]
    public void GetPolicy_MachineToMachine_AllowsOnlyConfidentialCredentialManagement()
    {
        ApplicationTypePolicy policy = ApplicationTypePolicyCatalog.GetPolicy(ApplicationType.MachineToMachine);

        policy.AllowedGrantTypes.ShouldBe(["client_credentials"]);
        policy.Options[ApplicationOptionKey.RedirectUris].ShouldBe(ApplicationOptionAvailability.Hidden);
        policy.Options[ApplicationOptionKey.PostLogoutRedirectUris].ShouldBe(ApplicationOptionAvailability.Hidden);
        policy.Options[ApplicationOptionKey.ClientSecrets].ShouldBe(ApplicationOptionAvailability.Available);
        policy.Options[ApplicationOptionKey.ClientCertificates].ShouldBe(ApplicationOptionAvailability.Advanced);
        policy.Options[ApplicationOptionKey.Consent].ShouldBe(ApplicationOptionAvailability.Hidden);
    }

    [Fact]
    public void GetPolicy_Device_IsReservedUntilDeviceFlowIsImplemented()
    {
        ApplicationTypePolicy policy = ApplicationTypePolicyCatalog.GetPolicy(ApplicationType.Device);

        policy.IsSelectable.ShouldBeFalse();
        policy.UnavailabilityReason.ShouldNotBeNullOrWhiteSpace();
        policy.DefaultClientProfile.ShouldBe(ClientProfile.Public);
        policy.DefaultGrantTypes.ShouldContain("urn:ietf:params:oauth:grant-type:device_code");
        policy.Options[ApplicationOptionKey.DeviceCodeGrant].ShouldBe(ApplicationOptionAvailability.ReadOnly);
    }

    [Fact]
    public void ConfigureOAuth_WhenTypeChangesAfterCreation_ReturnsTypeChangeNotAllowed()
    {
        Application application = Application.Create(
            "orders-web",
            "Orders Web",
            null,
            ApplicationType.Web,
            OAuthClientType.Confidential,
            ["authorization_code"],
            ["openid"],
            ["https://orders.example.com/callback"],
            [],
            requirePkce: true,
            requireConsent: true,
            this.dateTimeProvider).Value;

        Result result = application.ConfigureOAuth(
            ApplicationType.SinglePage,
            OAuthClientType.Public,
            ["authorization_code"],
            ["openid", "profile"],
            ["https://orders.example.com/callback"],
            [],
            requirePkce: true,
            requireConsent: true,
            this.dateTimeProvider);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ApplicationErrors.TypeChangeNotAllowed.Code);
    }
}
