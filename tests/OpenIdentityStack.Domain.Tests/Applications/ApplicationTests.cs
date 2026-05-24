using OpenIdentityStack.Domain.Applications;
using SharedKernel;

namespace OpenIdentityStack.Domain.Tests.Applications;

public sealed class ApplicationTests
{
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly DateTimeOffset now = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);

    public ApplicationTests()
    {
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.dateTimeProvider.UtcNow.Returns(this.now);
    }

    [Fact]
    public void Create_WithValidWebApplication_ReturnsActiveApplicationAndRaisesEvent()
    {
        Result<Application> result = Application.Create(
            "portal-web",
            "Portal Web",
            "Administrator portal",
            ApplicationType.Web,
            OAuthClientType.Confidential,
            ["authorization_code", "refresh_token"],
            ["openid", "profile"],
            ["https://portal.example.com/callback"],
            ["https://portal.example.com/signout-callback"],
            requirePkce: false,
            requireConsent: true,
            this.dateTimeProvider);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ClientId.ShouldBe("portal-web");
        result.Value.DisplayName.ShouldBe("Portal Web");
        result.Value.Description.ShouldBe("Administrator portal");
        result.Value.Type.ShouldBe(ApplicationType.Web);
        result.Value.ClientType.ShouldBe(OAuthClientType.Confidential);
        result.Value.Status.ShouldBe(ApplicationStatus.Active);
        result.Value.AllowedGrantTypes.ShouldBe(["authorization_code", "refresh_token"]);
        result.Value.AllowedScopes.ShouldBe(["openid", "profile"]);
        result.Value.CreatedAt.ShouldBe(this.now);
        result.Value.ModifiedAt.ShouldBe(this.now);
        result.Value.GetDomainEvents().ShouldContain(e => e is ApplicationDomainEvents.ApplicationCreated);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankClientId_ReturnsValidationFailure(string? clientId)
    {
        Result<Application> result = this.CreateValid(clientId: clientId!);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ApplicationErrors.ClientIdRequired.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankDisplayName_ReturnsValidationFailure(string? displayName)
    {
        Result<Application> result = this.CreateValid(displayName: displayName!);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ApplicationErrors.DisplayNameRequired.Code);
    }

    [Fact]
    public void UpdateMetadata_WithValidValues_UpdatesMetadataAndRaisesEvent()
    {
        Application application = this.CreateValid().Value;
        this.dateTimeProvider.UtcNow.Returns(this.now.AddMinutes(5));

        Result result = application.UpdateMetadata(" Updated App ", " Updated description ", this.dateTimeProvider);

        result.IsSuccess.ShouldBeTrue();
        application.DisplayName.ShouldBe("Updated App");
        application.Description.ShouldBe("Updated description");
        application.ModifiedAt.ShouldBe(this.now.AddMinutes(5));
        application.GetDomainEvents().ShouldContain(e => e is ApplicationDomainEvents.ApplicationMetadataUpdated);
    }

    [Fact]
    public void Disable_WhenActive_DisablesAndRaisesEvent()
    {
        Application application = this.CreateValid().Value;

        Result result = application.Disable(this.dateTimeProvider);

        result.IsSuccess.ShouldBeTrue();
        application.Status.ShouldBe(ApplicationStatus.Disabled);
        application.GetDomainEvents().ShouldContain(e => e is ApplicationDomainEvents.ApplicationDisabled);
    }

    [Fact]
    public void Disable_WhenAlreadyDisabled_ReturnsDomainError()
    {
        Application application = this.CreateValid().Value;
        application.Disable(this.dateTimeProvider);

        Result result = application.Disable(this.dateTimeProvider);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ApplicationErrors.AlreadyDisabled.Code);
    }

    [Fact]
    public void Enable_WhenDisabled_EnablesAndRaisesEvent()
    {
        Application application = this.CreateValid().Value;
        application.Disable(this.dateTimeProvider);

        Result result = application.Enable(this.dateTimeProvider);

        result.IsSuccess.ShouldBeTrue();
        application.Status.ShouldBe(ApplicationStatus.Active);
        application.GetDomainEvents().ShouldContain(e => e is ApplicationDomainEvents.ApplicationEnabled);
    }

    [Fact]
    public void Delete_RaisesDeletedEvent()
    {
        Application application = this.CreateValid().Value;

        Result result = application.Delete(this.dateTimeProvider);

        result.IsSuccess.ShouldBeTrue();
        application.GetDomainEvents().ShouldContain(e => e is ApplicationDomainEvents.ApplicationDeleted);
    }

    private Result<Application> CreateValid(
        string clientId = "orders-web",
        string displayName = "Orders Web") =>
        Application.Create(
            clientId,
            displayName,
            "Orders web application",
            ApplicationType.Web,
            OAuthClientType.Confidential,
            ["authorization_code"],
            ["openid", "orders.read"],
            ["https://orders.example.com/callback"],
            [],
            requirePkce: false,
            requireConsent: true,
            this.dateTimeProvider);
}
