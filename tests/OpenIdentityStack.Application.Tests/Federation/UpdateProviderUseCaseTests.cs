using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Federation.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Federation;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Federation;
/// <summary>
/// Unit tests for the UpdateProviderUseCase.
/// </summary>
public sealed class UpdateProviderUseCaseTests
{
    private readonly IUpstreamProviderRepository _providerRepository;
    private readonly UpdateProviderUseCase _useCase;
    private static readonly DateTimeOffset TestTime = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

    public UpdateProviderUseCaseTests()
    {
        this._providerRepository = Substitute.For<IUpstreamProviderRepository>();
        this._useCase = new UpdateProviderUseCase(this._providerRepository);
    }

    [Fact]
    public async Task ExecuteAsync_ProviderNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var command = new UpdateProviderCommand(Guid.NewGuid(), DisplayName: "Updated Name");
        this._providerRepository.GetByIdAsync(Arg.Any<UpstreamProviderId>(), Arg.Any<CancellationToken>())
            .Returns((UpstreamProvider?)null);

        // Act
        Result<UpdateProviderResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("ProviderNotFound");
    }

    [Fact]
    public async Task ExecuteAsync_UpdateDisplayName_ReturnsSuccess()
    {
        // Arrange
        UpstreamProvider provider = CreateTestProvider();
        var command = new UpdateProviderCommand(provider.Id.Value, DisplayName: "Updated Display Name");

        this._providerRepository.GetByIdAsync(provider.Id, Arg.Any<CancellationToken>())
            .Returns(provider);

        // Act
        Result<UpdateProviderResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
            result.Value.DisplayName.ShouldBe("Updated Display Name");
            await this._providerRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

    [Fact]
    public async Task ExecuteAsync_UpdateClientId_ReturnsSuccess()
    {
        // Arrange
        UpstreamProvider provider = CreateTestProvider();
        var command = new UpdateProviderCommand(provider.Id.Value, ClientId: "new-client-id");

        this._providerRepository.GetByIdAsync(provider.Id, Arg.Any<CancellationToken>())
            .Returns(provider);

        // Act
        Result<UpdateProviderResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ClientId.ShouldBe("new-client-id");
    }

    [Fact]
    public async Task ExecuteAsync_DisableProvider_ReturnsSuccess()
    {
        // Arrange
        UpstreamProvider provider = CreateTestProvider();
        var command = new UpdateProviderCommand(provider.Id.Value, Status: "Disabled");

        this._providerRepository.GetByIdAsync(provider.Id, Arg.Any<CancellationToken>())
            .Returns(provider);

        // Act
        Result<UpdateProviderResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe("Disabled");
    }

    [Fact]
    public async Task ExecuteAsync_EnableProvider_ReturnsSuccess()
    {
        // Arrange
        UpstreamProvider provider = CreateTestProvider();
        provider.Disable();
        var command = new UpdateProviderCommand(provider.Id.Value, Status: "Active");

        this._providerRepository.GetByIdAsync(provider.Id, Arg.Any<CancellationToken>())
            .Returns(provider);

        // Act
        Result<UpdateProviderResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe("Active");
    }

    [Fact]
    public async Task ExecuteAsync_UpdateClientSecret_Succeeds()
    {
        // Arrange
        UpstreamProvider provider = CreateTestProvider();
        var command = new UpdateProviderCommand(provider.Id.Value, ClientSecret: "new-secret");

        this._providerRepository.GetByIdAsync(provider.Id, Arg.Any<CancellationToken>())
            .Returns(provider);

        // Act
        Result<UpdateProviderResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await this._providerRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_MultipleUpdates_AppliesAll()
    {
        // Arrange
        UpstreamProvider provider = CreateTestProvider();
        var command = new UpdateProviderCommand(
            provider.Id.Value,
            DisplayName: "New Display Name",
            ClientId: "new-client-id",
            Status: "Disabled");

        this._providerRepository.GetByIdAsync(provider.Id, Arg.Any<CancellationToken>())
            .Returns(provider);

        // Act
        Result<UpdateProviderResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.DisplayName.ShouldBe("New Display Name");
        result.Value.ClientId.ShouldBe("new-client-id");
        result.Value.Status.ShouldBe("Disabled");
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancellationToken()
    {
        // Arrange
        UpstreamProvider provider = CreateTestProvider();
        var command = new UpdateProviderCommand(provider.Id.Value);
        using var cts = new CancellationTokenSource();

        this._providerRepository.GetByIdAsync(provider.Id, cts.Token)
            .Returns(provider);

        // Act
        await this._useCase.ExecuteAsync(command, cts.Token);

        // Assert
        await this._providerRepository.Received(1).GetByIdAsync(provider.Id, cts.Token);
        await this._providerRepository.Received(1).SaveChangesAsync(cts.Token);
    }

    private static UpstreamProvider CreateTestProvider()
    {
        Result<UpstreamProvider> result = UpstreamProvider.Create(
            "test-provider",
            "Test Provider",
            "https://authority.example.com",
            "client-id");

        return result.Value;
    }
}
