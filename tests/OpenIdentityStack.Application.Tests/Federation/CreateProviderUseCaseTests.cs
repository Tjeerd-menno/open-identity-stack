using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Federation.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Federation;

namespace OpenIdentityStack.Application.Tests.Federation;

public sealed class CreateProviderUseCaseTests
{
    private readonly IUpstreamProviderRepository providerRepository;
    private readonly IEnvironmentProvider environmentProvider;
    private readonly ISecretProtector secretProtector;
    private readonly CreateProviderUseCase useCase;

    public CreateProviderUseCaseTests()
    {
        this.providerRepository = Substitute.For<IUpstreamProviderRepository>();
        this.environmentProvider = Substitute.For<IEnvironmentProvider>();
        this.secretProtector = Substitute.For<ISecretProtector>();
        this.secretProtector.Protect(Arg.Any<string>()).Returns(call => $"protected:{call.Arg<string>()}");
        this.useCase = new CreateProviderUseCase(
            this.providerRepository,
            this.environmentProvider,
            this.secretProtector);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidCommand_AddsProviderAndReturnsMappedResult()
    {
        var command = new CreateProviderCommand(
            " Google ",
            " Google Identity ",
            "https://accounts.example.com/",
            " client-id ",
            null,
            ["openid", "profile"]);

        UpstreamProvider? addedProvider = null;
        this.providerRepository.GetByNameAsync(command.Name, Arg.Any<CancellationToken>())
            .Returns((UpstreamProvider?)null);
        this.providerRepository.AddAsync(Arg.Do<UpstreamProvider>(provider => addedProvider = provider), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        Result<CreateProviderResult> result = await this.useCase.ExecuteAsync(command);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("google");
        result.Value.DisplayName.ShouldBe("Google Identity");
        result.Value.Authority.ShouldBe("https://accounts.example.com");
        result.Value.ClientId.ShouldBe("client-id");
        result.Value.Scopes.ShouldBe(["openid", "profile"]);
        result.Value.JitProvisioningEnabled.ShouldBeTrue();
        result.Value.Status.ShouldBe(ProviderStatus.Active.ToString());
        addedProvider.ShouldNotBeNull();
        addedProvider.Name.ShouldBe("google");
        await this.providerRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenProviderNameExists_ReturnsConflictAndDoesNotPersist()
    {
        UpstreamProvider existingProvider = CreateProvider("google");
        var command = new CreateProviderCommand(
            existingProvider.Name,
            "Google",
            "https://accounts.example.com",
            "client-id",
            null,
            null);

        this.providerRepository.GetByNameAsync(existingProvider.Name, Arg.Any<CancellationToken>())
            .Returns(existingProvider);

        Result<CreateProviderResult> result = await this.useCase.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.ProviderNameExists");
        await this.providerRepository.DidNotReceive().AddAsync(Arg.Any<UpstreamProvider>(), Arg.Any<CancellationToken>());
        await this.providerRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithHttpAuthorityOutsideDevelopment_ReturnsValidationError()
    {
        var command = new CreateProviderCommand(
            "local",
            "Local",
            "http://localhost:8080",
            "client-id",
            null,
            null);

        this.environmentProvider.IsDevelopment.Returns(false);

        Result<CreateProviderResult> result = await this.useCase.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UpstreamProviderErrors.HttpNotAllowed.Code);
        await this.providerRepository.DidNotReceive().AddAsync(Arg.Any<UpstreamProvider>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithHttpAuthorityInDevelopment_PersistsProvider()
    {
        var command = new CreateProviderCommand(
            "local",
            "Local",
            "http://localhost:8080",
            "client-id",
            null,
            null);

        this.environmentProvider.IsDevelopment.Returns(true);

        Result<CreateProviderResult> result = await this.useCase.ExecuteAsync(command);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Authority.ShouldBe("http://localhost:8080");
        await this.providerRepository.Received(1).AddAsync(Arg.Any<UpstreamProvider>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithClientSecret_ProtectsSecretBeforePersisting()
    {
        var command = new CreateProviderCommand(
            "google",
            "Google",
            "https://accounts.example.com",
            "client-id",
            "plain-secret",
            null);

        UpstreamProvider? addedProvider = null;
        this.providerRepository.AddAsync(Arg.Do<UpstreamProvider>(provider => addedProvider = provider), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        Result<CreateProviderResult> result = await this.useCase.ExecuteAsync(command);

        result.IsSuccess.ShouldBeTrue();
        this.secretProtector.Received(1).Protect("plain-secret");
        addedProvider.ShouldNotBeNull();
        addedProvider.ClientSecret.ShouldBe("protected:plain-secret");
    }

    [Fact]
    public async Task ExecuteAsync_WhenProtectingClientSecretFails_ReturnsFailureAndDoesNotPersist()
    {
        var command = new CreateProviderCommand(
            "google",
            "Google",
            "https://accounts.example.com",
            "client-id",
            "plain-secret",
            null);

        this.secretProtector.Protect("plain-secret").Returns(_ => throw new InvalidOperationException("failed"));

        Result<CreateProviderResult> result = await this.useCase.ExecuteAsync(command);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Failure.ProviderClientSecretProtectionFailed");
        await this.providerRepository.DidNotReceive().AddAsync(Arg.Any<UpstreamProvider>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancellationTokenToRepository()
    {
        var command = new CreateProviderCommand(
            "google",
            "Google",
            "https://accounts.example.com",
            "client-id",
            null,
            null);
        using var cts = new CancellationTokenSource();

        await this.useCase.ExecuteAsync(command, cts.Token);

        await this.providerRepository.Received(1).GetByNameAsync(command.Name, cts.Token);
        await this.providerRepository.Received(1).AddAsync(Arg.Any<UpstreamProvider>(), cts.Token);
        await this.providerRepository.Received(1).SaveChangesAsync(cts.Token);
    }

    private static UpstreamProvider CreateProvider(string name)
    {
        return UpstreamProvider.Create(
            name,
            "Provider",
            "https://authority.example.com",
            "client-id").Value;
    }
}
