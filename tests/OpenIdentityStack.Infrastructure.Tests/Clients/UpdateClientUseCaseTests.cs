using OpenIdentityStack.Application.Clients;
using OpenIdentityStack.Application.Clients.Commands;
using OpenIdentityStack.Domain.Clients;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Infrastructure.Clients;
using OpenIddict.Abstractions;

namespace OpenIdentityStack.Infrastructure.Tests.Clients;

public sealed class UpdateClientUseCaseTests
{
    private readonly IClientRepository _clientRepository;
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly UpdateClientUseCase _sut;

    public UpdateClientUseCaseTests()
    {
        this._clientRepository = Substitute.For<IClientRepository>();
        this._applicationManager = Substitute.For<IOpenIddictApplicationManager>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2026, 4, 30, 12, 0, 0, TimeSpan.Zero));
        this._sut = new UpdateClientUseCase(
            this._clientRepository,
            this._applicationManager,
            this._dateTimeProvider);
    }

    [Fact]
    public async Task ExecuteAsync_WhenClientAndOpenIddictApplicationExist_UpdatesBothAndSavesChanges()
    {
        Client client = CreateClient();
        object application = new();
        var command = new UpdateClientCommand(client.Id, "Updated Client", "Updated description");

        this._clientRepository.GetByIdAsync(client.Id, Arg.Any<CancellationToken>())
            .Returns(client);
        this._applicationManager.FindByClientIdAsync(client.ClientIdValue, Arg.Any<CancellationToken>())
            .Returns(application);

        Result<UpdateClientResult> result = await this._sut.ExecuteAsync(command);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ClientId.ShouldBe(client.Id);
        result.Value.DisplayName.ShouldBe("Updated Client");
        result.Value.Description.ShouldBe("Updated description");
        client.DisplayName.ShouldBe("Updated Client");
        client.Description.ShouldBe("Updated description");
        await this._applicationManager.Received(1).PopulateAsync(
            Arg.Any<OpenIddictApplicationDescriptor>(),
            application,
            Arg.Any<CancellationToken>());
        await this._applicationManager.Received(1).UpdateAsync(
            application,
            Arg.Is<OpenIddictApplicationDescriptor>(descriptor => descriptor.DisplayName == "Updated Client"),
            Arg.Any<CancellationToken>());
        await this._clientRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenOpenIddictApplicationIsMissing_UpdatesDomainClientAndSavesChanges()
    {
        Client client = CreateClient();
        var command = new UpdateClientCommand(client.Id, "Updated Client", null);

        this._clientRepository.GetByIdAsync(client.Id, Arg.Any<CancellationToken>())
            .Returns(client);
        this._applicationManager.FindByClientIdAsync(client.ClientIdValue, Arg.Any<CancellationToken>())
            .Returns((object?)null);

        Result<UpdateClientResult> result = await this._sut.ExecuteAsync(command);

        result.IsSuccess.ShouldBeTrue();
        result.Value.DisplayName.ShouldBe("Updated Client");
        result.Value.Description.ShouldBeNull();
        await this._applicationManager.DidNotReceive().UpdateAsync(
            Arg.Any<object>(),
            Arg.Any<OpenIddictApplicationDescriptor>(),
            Arg.Any<CancellationToken>());
        await this._clientRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenClientIsMissing_ReturnsNotFoundAndDoesNotSave()
    {
        var clientId = ClientId.NewId();
        var command = new UpdateClientCommand(clientId, "Updated Client", null);

        this._clientRepository.GetByIdAsync(clientId, Arg.Any<CancellationToken>())
            .Returns((Client?)null);

        Result<UpdateClientResult> result = await this._sut.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ClientErrors.NotFound);
        await this._applicationManager.DidNotReceive()
            .FindByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await this._clientRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDisplayNameIsEmpty_ReturnsValidationErrorAndDoesNotSave()
    {
        Client client = CreateClient();
        var command = new UpdateClientCommand(client.Id, string.Empty, "Updated description");

        this._clientRepository.GetByIdAsync(client.Id, Arg.Any<CancellationToken>())
            .Returns(client);

        Result<UpdateClientResult> result = await this._sut.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ClientErrors.DisplayNameRequired);
        await this._applicationManager.DidNotReceive()
            .FindByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await this._clientRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private Client CreateClient()
    {
        Result<Client> result = Client.Create(
            "test-client",
            "Test Client",
            ClientType.Confidential,
            "A test client",
            ["https://example.com/callback"],
            ["https://example.com/logout"],
            ["openid", "profile"],
            ["authorization_code"],
            true,
            false,
            this._dateTimeProvider);

        return result.Value;
    }
}
