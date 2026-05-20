using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Federation.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Federation;

namespace OpenIdentityStack.Application.Tests.Federation;

public sealed class ListProvidersQueryHandlerTests
{
    private readonly IUpstreamProviderRepository providerRepository;
    private readonly ListProvidersQueryHandler handler;

    public ListProvidersQueryHandlerTests()
    {
        this.providerRepository = Substitute.For<IUpstreamProviderRepository>();
        this.handler = new ListProvidersQueryHandler(this.providerRepository);
    }

    [Fact]
    public async Task HandleAsync_WhenIncludeDisabledIsFalse_UsesActiveProviders()
    {
        UpstreamProvider google = CreateProvider("google", "Google");
        UpstreamProvider github = CreateProvider("github", "GitHub");
        this.providerRepository.GetActiveProvidersAsync(Arg.Any<CancellationToken>())
            .Returns([google, github]);

        Result<IReadOnlyList<ProviderDto>> result = await this.handler.HandleAsync(new ListProvidersQuery());

        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2);
        result.Value[0].Id.ShouldBe(google.Id.Value);
        result.Value[0].Name.ShouldBe(google.Name);
        result.Value[0].DisplayName.ShouldBe(google.DisplayName);
        result.Value[0].Authority.ShouldBe(google.Authority);
        result.Value[0].ClientId.ShouldBe(google.ClientId);
        result.Value[0].Scopes.ShouldBeEmpty();
        result.Value[0].JitProvisioningEnabled.ShouldBeTrue();
        result.Value[0].Status.ShouldBe(ProviderStatus.Active.ToString());
        await this.providerRepository.Received(1).GetActiveProvidersAsync(Arg.Any<CancellationToken>());
        await this.providerRepository.DidNotReceive().GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenIncludeDisabledIsTrue_UsesAllProviders()
    {
        UpstreamProvider activeProvider = CreateProvider("google", "Google");
        UpstreamProvider disabledProvider = CreateProvider("github", "GitHub");
        disabledProvider.Disable();
        this.providerRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([activeProvider, disabledProvider]);

        Result<IReadOnlyList<ProviderDto>> result = await this.handler.HandleAsync(new ListProvidersQuery(IncludeDisabled: true));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2);
        result.Value[1].Status.ShouldBe(ProviderStatus.Disabled.ToString());
        await this.providerRepository.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
        await this.providerRepository.DidNotReceive().GetActiveProvidersAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenNoProviders_ReturnsEmptyList()
    {
        this.providerRepository.GetActiveProvidersAsync(Arg.Any<CancellationToken>())
            .Returns([]);

        Result<IReadOnlyList<ProviderDto>> result = await this.handler.HandleAsync(new ListProvidersQuery());

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_PassesCancellationToken()
    {
        using var cts = new CancellationTokenSource();

        await this.handler.HandleAsync(new ListProvidersQuery(IncludeDisabled: true), cts.Token);

        await this.providerRepository.Received(1).GetAllAsync(cts.Token);
    }

    private static UpstreamProvider CreateProvider(string name, string displayName)
    {
        return UpstreamProvider.Create(
            name,
            displayName,
            $"https://{name}.example.com",
            $"{name}-client").Value;
    }
}
