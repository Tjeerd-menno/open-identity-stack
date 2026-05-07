using OpenIdentityStack.Application.Clients;
using OpenIdentityStack.Application.Clients.Commands;
using OpenIdentityStack.Domain.Clients;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Infrastructure.Clients;
using OpenIddict.Abstractions;

namespace OpenIdentityStack.Infrastructure.Tests.Clients;

public sealed class DeleteClientUseCaseTests
{
    private readonly IClientRepository _clientRepository;
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly DeleteClientUseCase _sut;
    private readonly IDateTimeProvider _dateTimeProvider;

    public DeleteClientUseCaseTests()
    {
        this._clientRepository = Substitute.For<IClientRepository>();
        this._applicationManager = Substitute.For<IOpenIddictApplicationManager>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2026, 4, 30, 12, 0, 0, TimeSpan.Zero));
        this._sut = new DeleteClientUseCase(this._clientRepository, this._applicationManager);
    }

    [Fact]
    public async Task ExecuteAsync_WhenClientExistsWithOpenIddictApplication_DeletesApplicationAndClient()
    {
        Client client = CreateClient();
        object application = new();
        var command = new DeleteClientCommand(client.Id);

        this._clientRepository.GetByIdAsync(client.Id, Arg.Any<CancellationToken>())
            .Returns(client);
        this._applicationManager.FindByClientIdAsync(client.ClientIdValue, Arg.Any<CancellationToken>())
            .Returns(application);

        Result result = await this._sut.ExecuteAsync(command);

        result.IsSuccess.ShouldBeTrue();
        await this._applicationManager.Received(1).DeleteAsync(application, Arg.Any<CancellationToken>());
        await this._clientRepository.Received(1).DeleteAsync(client, Arg.Any<CancellationToken>());
        await this._clientRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenClientExistsWithoutOpenIddictApplication_DeletesClientOnly()
    {
        Client client = CreateClient();
        var command = new DeleteClientCommand(client.Id);

        this._clientRepository.GetByIdAsync(client.Id, Arg.Any<CancellationToken>())
            .Returns(client);
        this._applicationManager.FindByClientIdAsync(client.ClientIdValue, Arg.Any<CancellationToken>())
            .Returns((object?)null);

        Result result = await this._sut.ExecuteAsync(command);

        result.IsSuccess.ShouldBeTrue();
        await this._applicationManager.DidNotReceive().DeleteAsync(Arg.Any<object>(), Arg.Any<CancellationToken>());
        await this._clientRepository.Received(1).DeleteAsync(client, Arg.Any<CancellationToken>());
        await this._clientRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenClientIsMissing_ReturnsNotFoundAndDoesNotDelete()
    {
        var clientId = ClientId.NewId();
        var command = new DeleteClientCommand(clientId);

        this._clientRepository.GetByIdAsync(clientId, Arg.Any<CancellationToken>())
            .Returns((Client?)null);

        Result result = await this._sut.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ClientErrors.NotFound);
        await this._applicationManager.DidNotReceive()
            .FindByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await this._clientRepository.DidNotReceive().DeleteAsync(Arg.Any<Client>(), Arg.Any<CancellationToken>());
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
