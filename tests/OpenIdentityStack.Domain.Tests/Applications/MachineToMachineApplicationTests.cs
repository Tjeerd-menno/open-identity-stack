using OpenIdentityStack.Domain.Applications;
using SharedKernel;

namespace OpenIdentityStack.Domain.Tests.Applications;

public sealed class MachineToMachineApplicationTests
{
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly DateTimeOffset now = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);

    public MachineToMachineApplicationTests()
    {
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.dateTimeProvider.UtcNow.Returns(this.now);
    }

    [Fact]
    public void CreateMachineToMachine_WithScopes_AppliesSafeDefaults()
    {
        Result<Application> result = Application.CreateMachineToMachine(
            "orders-worker",
            "Orders Worker",
            "Background order processor",
            ["orders.read", "orders.write"],
            this.dateTimeProvider);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Type.ShouldBe(ApplicationType.MachineToMachine);
        result.Value.ClientType.ShouldBe(OAuthClientType.Confidential);
        result.Value.AllowedGrantTypes.ShouldBe(["client_credentials"]);
        result.Value.AllowedScopes.ShouldBe(["orders.read", "orders.write"]);
        result.Value.RedirectUris.ShouldBeEmpty();
        result.Value.PostLogoutRedirectUris.ShouldBeEmpty();
        result.Value.RequirePkce.ShouldBeFalse();
        result.Value.RequireConsent.ShouldBeFalse();
    }

    [Fact]
    public void ConfigureOAuth_MachineToMachineWithInteractiveGrant_ReturnsInvalidGrantType()
    {
        Application application = Application.CreateMachineToMachine(
            "orders-worker",
            "Orders Worker",
            null,
            ["orders.read"],
            this.dateTimeProvider).Value;

        Result result = application.ConfigureOAuth(
            ApplicationType.MachineToMachine,
            OAuthClientType.Confidential,
            ["authorization_code"],
            ["orders.read"],
            ["https://worker.example.com/callback"],
            [],
            requirePkce: false,
            requireConsent: true,
            this.dateTimeProvider);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ApplicationErrors.InvalidGrantType.Code);
    }
}
