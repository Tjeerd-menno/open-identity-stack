using OpenIdentityStack.Application.Clients;
using OpenIdentityStack.Application.Clients.Commands;
using OpenIdentityStack.Domain.Clients;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Infrastructure.Clients;
using OpenIddict.Abstractions;

using SharedKernel;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;
using IApplicationProtocolProjection = OpenIdentityStack.Application.Abstractions.IApplicationProtocolProjection;
#pragma warning disable IDE0007 // Use var instead of explicit type
#pragma warning disable IDE0008 // Use explicit type instead of var

namespace OpenIdentityStack.Infrastructure.Tests.Clients;

/// <summary>
/// Tests for CreateClientUseCase.
/// </summary>
public sealed class CreateClientUseCaseTests
{
    private readonly IClientRepository _clientRepository;
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IApplicationProtocolProjection _projection;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly CreateClientUseCase _useCase;
    private readonly DateTimeOffset _now = new(2026, 2, 3, 12, 0, 0, TimeSpan.Zero);

    public CreateClientUseCaseTests()
    {
        this._clientRepository = Substitute.For<IClientRepository>();
        this._applicationManager = Substitute.For<IOpenIddictApplicationManager>();
        this._projection = Substitute.For<IApplicationProtocolProjection>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(this._now);
        this._projection.UpsertAsync(Arg.Any<DomainApplication>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        this._useCase = new CreateClientUseCase(
            this._clientRepository,
            this._applicationManager,
            this._projection,
            this._dateTimeProvider);
    }

    #region Success Cases

    [Fact]
    public async Task ExecuteAsync_WithValidConfidentialClient_ReturnsSuccessWithSecret()
    {
        // Arrange
        var command = new CreateClientCommand(
            "test-client",
            "Test Client",
            ClientType.Confidential,
            "A test client",
            ["https://example.com/callback"],
            ["https://example.com/logout"],
            ["openid", "profile"],
            ["authorization_code"],
            true,
            false);

        this._clientRepository.GetByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns((Client?)null);
        this._applicationManager.FindByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns((object?)null);

        // Act
        Result<CreateClientResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ClientIdValue.ShouldBe("test-client");
        result.Value.DisplayName.ShouldBe("Test Client");
        result.Value.ClientSecret.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithValidPublicClient_ReturnsSuccessWithoutSecret()
    {
        // Arrange
        var command = new CreateClientCommand(
            "public-client",
            "Public Client",
            ClientType.Public,
            null,
            ["https://example.com/callback"],
            [],
            ["openid"],
            ["authorization_code"],
            true,
            false);

        this._clientRepository.GetByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns((Client?)null);
        this._applicationManager.FindByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns((object?)null);

        // Act
        Result<CreateClientResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ClientSecret.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_AddsClientToRepository()
    {
        // Arrange
        var command = new CreateClientCommand(
            "test-client",
            "Test Client",
            ClientType.Confidential,
            null,
            ["https://example.com/callback"],
            [],
            ["openid"],
            ["authorization_code"],
            true,
            false);

        this._clientRepository.GetByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns((Client?)null);
        this._applicationManager.FindByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns((object?)null);

        // Act
        Result<CreateClientResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        await this._clientRepository.Received(1).AddAsync(
            Arg.Is<Client>(c => c.ClientIdValue == "test-client"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SavesChanges()
    {
        // Arrange
        var command = new CreateClientCommand(
            "test-client",
            "Test Client",
            ClientType.Confidential,
            null,
            ["https://example.com/callback"],
            [],
            ["openid"],
            ["authorization_code"],
            true,
            false);

        this._clientRepository.GetByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns((Client?)null);
        this._applicationManager.FindByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns((object?)null);

        // Act
        Result<CreateClientResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        await this._clientRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ProjectsUnifiedApplication()
    {
        // Arrange
        var command = new CreateClientCommand(
            "test-client",
            "Test Client",
            ClientType.Confidential,
            null,
            ["https://example.com/callback"],
            ["https://example.com/logout"],
            ["openid", "profile"],
            ["authorization_code"],
            true,
            false);

        this._clientRepository.GetByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns((Client?)null);
        this._applicationManager.FindByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns((object?)null);

        // Act
        Result<CreateClientResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        await this._projection.Received(1).UpsertAsync(
            Arg.Is<DomainApplication>(application =>
                application.ClientId == "test-client" &&
                application.DisplayName == "Test Client" &&
                application.ClientType == OpenIdentityStack.Domain.Applications.OAuthClientType.Confidential &&
                application.RedirectUris.Count == 1 &&
                application.RedirectUris[0] == "https://example.com/callback" &&
                application.PostLogoutRedirectUris.Count == 1 &&
                application.PostLogoutRedirectUris[0] == "https://example.com/logout" &&
                !string.IsNullOrEmpty(result.Value.ClientSecret)),
            Arg.Is<string>(secret => secret == result.Value.ClientSecret),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithClientCredentialsFlow_SetsCorrectPermissions()
    {
        // Arrange
        var command = new CreateClientCommand(
            "service-client",
            "Service Client",
            ClientType.Confidential,
            null,
            [],
            [],
            ["openid"],
            ["client_credentials"],
            false,
            false);

        this._clientRepository.GetByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns((Client?)null);
        this._applicationManager.FindByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns((object?)null);

        // Act
        await this._useCase.ExecuteAsync(command);

        // Assert
        await this._projection.Received(1).UpsertAsync(
            Arg.Is<DomainApplication>(application =>
                application.Profile == OpenIdentityStack.Domain.Applications.ApplicationProfile.MachineToMachine &&
                application.ClientType == OpenIdentityStack.Domain.Applications.OAuthClientType.Confidential &&
                application.AllowedGrantTypes.SequenceEqual(new[] { OpenIddictConstants.GrantTypes.ClientCredentials })),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithRefreshToken_SetsCorrectPermissions()
    {
        // Arrange
        var command = new CreateClientCommand(
            "refresh-client",
            "Refresh Client",
            ClientType.Confidential,
            null,
            ["https://example.com/callback"],
            [],
            ["openid", "offline_access"],
            ["authorization_code", "refresh_token"],
            true,
            false);

        this._clientRepository.GetByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns((Client?)null);
        this._applicationManager.FindByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns((object?)null);

        // Act
        await this._useCase.ExecuteAsync(command);

        // Assert
        await this._projection.Received(1).UpsertAsync(
            Arg.Is<DomainApplication>(application =>
                application.AllowedGrantTypes.Contains(OpenIddictConstants.GrantTypes.RefreshToken)),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Failure Cases

    [Fact]
    public async Task ExecuteAsync_WithDuplicateClientIdInRepository_ReturnsError()
    {
        // Arrange
        var command = new CreateClientCommand(
            "existing-client",
            "Existing Client",
            ClientType.Confidential,
            null,
            ["https://example.com/callback"],
            [],
            ["openid"],
            ["authorization_code"],
            true,
            false);

        Result<Client> existingResult = Client.Create(
            "existing-client",
            "Existing",
            ClientType.Confidential,
            null,
            ["https://example.com/callback"],
            [],
            ["openid"],
            ["authorization_code"],
            true,
            false,
            this._dateTimeProvider);

        this._clientRepository.GetByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns(existingResult.Value);

        // Act
        Result<CreateClientResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ClientErrors.ClientIdExists);
    }

    [Fact]
    public async Task ExecuteAsync_WithDuplicateClientIdInOpenIddict_ReturnsError()
    {
        // Arrange
        var command = new CreateClientCommand(
            "existing-client",
            "Existing Client",
            ClientType.Confidential,
            null,
            ["https://example.com/callback"],
            [],
            ["openid"],
            ["authorization_code"],
            true,
            false);

        this._clientRepository.GetByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns((Client?)null);
        this._applicationManager.FindByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns(new object()); // Some existing app

        // Act
        Result<CreateClientResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ClientErrors.ClientIdExists);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyClientId_ReturnsValidationError()
    {
        // Arrange
        var command = new CreateClientCommand(
            "",
            "Test Client",
            ClientType.Confidential,
            null,
            ["https://example.com/callback"],
            [],
            ["openid"],
            ["authorization_code"],
            true,
            false);

        this._clientRepository.GetByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns((Client?)null);
        this._applicationManager.FindByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns((object?)null);

        // Act
        Result<CreateClientResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ClientErrors.ClientIdRequired);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyDisplayName_ReturnsValidationError()
    {
        // Arrange
        var command = new CreateClientCommand(
            "test-client",
            "",
            ClientType.Confidential,
            null,
            ["https://example.com/callback"],
            [],
            ["openid"],
            ["authorization_code"],
            true,
            false);

        this._clientRepository.GetByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns((Client?)null);
        this._applicationManager.FindByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns((object?)null);

        // Act
        Result<CreateClientResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ClientErrors.DisplayNameRequired);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidRedirectUri_ReturnsValidationError()
    {
        // Arrange
        var command = new CreateClientCommand(
            "test-client",
            "Test Client",
            ClientType.Confidential,
            null,
            ["not-a-valid-uri"],
            [],
            ["openid"],
            ["authorization_code"],
            true,
            false);

        this._clientRepository.GetByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns((Client?)null);
        this._applicationManager.FindByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns((object?)null);

        // Act
        Result<CreateClientResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ClientErrors.InvalidRedirectUri);
    }

    [Fact]
    public async Task ExecuteAsync_WithAuthorizationCodeButNoRedirectUri_ReturnsValidationError()
    {
        // Arrange
        var command = new CreateClientCommand(
            "test-client",
            "Test Client",
            ClientType.Confidential,
            null,
            [],
            [],
            ["openid"],
            ["authorization_code"],
            true,
            false);

        this._clientRepository.GetByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns((Client?)null);
        this._applicationManager.FindByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns((object?)null);

        // Act
        Result<CreateClientResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ClientErrors.RedirectUriRequired);
    }

    #endregion

    #region PKCE Cases

    [Fact]
    public async Task ExecuteAsync_WithPublicClient_SetsPkceRequirement()
    {
        // Arrange
        var command = new CreateClientCommand(
            "public-client",
            "Public Client",
            ClientType.Public,
            null,
            ["https://example.com/callback"],
            [],
            ["openid"],
            ["authorization_code"],
            false, // RequirePkce is false but should still be set for public clients
            false);

        this._clientRepository.GetByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns((Client?)null);
        this._applicationManager.FindByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns((object?)null);

        // Act
        await this._useCase.ExecuteAsync(command);

        // Assert
        await this._projection.Received(1).UpsertAsync(
            Arg.Is<DomainApplication>(application => application.RequirePkce),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithExplicitPkceRequirement_SetsPkceRequirement()
    {
        // Arrange
        var command = new CreateClientCommand(
            "pkce-client",
            "PKCE Client",
            ClientType.Confidential,
            null,
            ["https://example.com/callback"],
            [],
            ["openid"],
            ["authorization_code"],
            true, // RequirePkce is true
            false);

        this._clientRepository.GetByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns((Client?)null);
        this._applicationManager.FindByClientIdAsync(command.ClientId, Arg.Any<CancellationToken>())
            .Returns((object?)null);

        // Act
        await this._useCase.ExecuteAsync(command);

        // Assert
        await this._projection.Received(1).UpsertAsync(
            Arg.Is<DomainApplication>(application => application.RequirePkce),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    #endregion
}
