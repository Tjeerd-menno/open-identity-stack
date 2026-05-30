using OpenIdentityStack.Domain.Applications;
using SharedKernel;

namespace OpenIdentityStack.Domain.Tests.Applications;

public sealed class ApplicationProfilePolicyTests
{
    private readonly IDateTimeProvider dateTimeProvider;

    public ApplicationProfilePolicyTests()
    {
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2026, 5, 24, 12, 0, 0, TimeSpan.Zero));
    }

    [Theory]
    [InlineData(ApplicationProfile.Web, ClientProfile.Confidential, "authorization_code", true, true, true)]
    [InlineData(ApplicationProfile.SinglePage, ClientProfile.Public, "authorization_code", true, true, true)]
    [InlineData(ApplicationProfile.Native, ClientProfile.Public, "authorization_code", true, true, true)]
    [InlineData(ApplicationProfile.MachineToMachine, ClientProfile.Confidential, "client_credentials", false, false, true)]
    public void GetPolicy_ForSupportedProfile_ReturnsExpectedDefaults(
        ApplicationProfile profile,
        ClientProfile expectedProfile,
        string expectedDefaultGrant,
        bool expectedRedirects,
        bool expectedConsent,
        bool expectedSelectable)
    {
        ApplicationProfilePolicy policy = ApplicationProfilePolicyCatalog.GetPolicy(profile);

        policy.ApplicationProfile.ShouldBe(profile);
        policy.DefaultClientProfile.ShouldBe(expectedProfile);
        policy.DefaultGrantTypes.ShouldContain(expectedDefaultGrant);
        policy.RequiresRedirectUris.ShouldBe(expectedRedirects);
        policy.DefaultRequireConsent.ShouldBe(expectedConsent);
        policy.IsSelectable.ShouldBe(expectedSelectable);
    }

    [Fact]
    public void GetPolicy_Web_ExposesExpectedOptionAvailability()
    {
        ApplicationProfilePolicy policy = ApplicationProfilePolicyCatalog.GetPolicy(ApplicationProfile.Web);

        policy.Options[ApplicationOptionKey.ClientSecrets].ShouldBe(ApplicationOptionAvailability.Available);
        policy.Options[ApplicationOptionKey.AuthorizationCodeGrant].ShouldBe(ApplicationOptionAvailability.Available);
        policy.Options[ApplicationOptionKey.ClientCredentialsGrant].ShouldBe(ApplicationOptionAvailability.Hidden);
        policy.Options[ApplicationOptionKey.Pkce].ShouldBe(ApplicationOptionAvailability.Available);
        policy.Options[ApplicationOptionKey.PostLogoutRedirectUris].ShouldBe(ApplicationOptionAvailability.Available);
    }

    [Fact]
    public void GetPolicy_SinglePage_HidesCredentialsAndRequiresPkce()
    {
        ApplicationProfilePolicy policy = ApplicationProfilePolicyCatalog.GetPolicy(ApplicationProfile.SinglePage);

        policy.Options[ApplicationOptionKey.ClientProfile].ShouldBe(ApplicationOptionAvailability.ReadOnly);
        policy.Options[ApplicationOptionKey.ClientSecrets].ShouldBe(ApplicationOptionAvailability.Hidden);
        policy.Options[ApplicationOptionKey.ClientCertificates].ShouldBe(ApplicationOptionAvailability.Hidden);
        policy.Options[ApplicationOptionKey.Pkce].ShouldBe(ApplicationOptionAvailability.ReadOnly);
        policy.RequirePkce.ShouldBeTrue();
    }

    [Fact]
    public void GetPolicy_Native_SupportsNativeRedirectPatterns()
    {
        ApplicationProfilePolicy policy = ApplicationProfilePolicyCatalog.GetPolicy(ApplicationProfile.Native);

        policy.Options[ApplicationOptionKey.NativeRedirectUriPatterns].ShouldBe(ApplicationOptionAvailability.Available);
        policy.Options[ApplicationOptionKey.LoopbackRedirectUri].ShouldBe(ApplicationOptionAvailability.Available);
        policy.Options[ApplicationOptionKey.ClientSecrets].ShouldBe(ApplicationOptionAvailability.Hidden);
        policy.RequirePkce.ShouldBeTrue();
    }

    [Fact]
    public void GetPolicy_MachineToMachine_AllowsOnlyConfidentialCredentialManagement()
    {
        ApplicationProfilePolicy policy = ApplicationProfilePolicyCatalog.GetPolicy(ApplicationProfile.MachineToMachine);

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
        ApplicationProfilePolicy policy = ApplicationProfilePolicyCatalog.GetPolicy(ApplicationProfile.Device);

        policy.IsSelectable.ShouldBeFalse();
        policy.UnavailabilityReason.ShouldNotBeNullOrWhiteSpace();
        policy.DefaultClientProfile.ShouldBe(ClientProfile.Public);
        policy.DefaultGrantTypes.ShouldContain("urn:ietf:params:oauth:grant-type:device_code");
        policy.Options[ApplicationOptionKey.DeviceCodeGrant].ShouldBe(ApplicationOptionAvailability.ReadOnly);
    }

    [Fact]
    public void ConfigureOAuth_WhenTypeChangesAfterCreation_ReturnsProfileChangeNotAllowed()
    {
        Application application = Application.Create(
            "orders-web",
            "Orders Web",
            null,
            ApplicationProfile.Web,
            OAuthClientType.Confidential,
            ["authorization_code"],
            ["openid"],
            ["https://orders.example.com/callback"],
            [],
            requirePkce: true,
            requireConsent: true,
            this.dateTimeProvider).Value;

        Result result = application.ConfigureOAuth(
            ApplicationProfile.SinglePage,
            OAuthClientType.Public,
            ["authorization_code"],
            ["openid", "profile"],
            ["https://orders.example.com/callback"],
            [],
            requirePkce: true,
            requireConsent: true,
            this.dateTimeProvider);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ApplicationErrors.ProfileChangeNotAllowed.Code);
    }
}
