using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Common;
using OpenIdentityStack.Application.ServiceAccounts.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ServiceAccounts;

namespace OpenIdentityStack.Application.Tests.ServiceAccounts;

public sealed class ServiceAccountQueryHandlerTests
{
    private readonly IServiceAccountRepository repository;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly ListServiceAccountsQueryHandler listHandler;
    private readonly GetServiceAccountQueryHandler getHandler;
    private readonly DateTimeOffset now = new(2026, 5, 20, 10, 0, 0, TimeSpan.Zero);

    public ServiceAccountQueryHandlerTests()
    {
        this.repository = Substitute.For<IServiceAccountRepository>();
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.dateTimeProvider.UtcNow.Returns(this.now);
        this.listHandler = new ListServiceAccountsQueryHandler(this.repository);
        this.getHandler = new GetServiceAccountQueryHandler(this.repository);
    }

    [Fact]
    public async Task ListHandleAsync_ReturnsMappedSummariesAndPaginationMetadata()
    {
        ServiceAccount first = this.CreateServiceAccount("reports-api", "Reports API");
        ServiceAccount second = this.CreateServiceAccount("billing-api", "Billing API");
        first.AddCredential("secret-hash", "primary secret", null, this.dateTimeProvider);
        first.AddCertificate(
            "0123456789ABCDEF0123456789ABCDEF01234567",
            "CN=reports-api",
            this.now.AddYears(1),
            this.dateTimeProvider);
        second.Disable(this.dateTimeProvider);
        this.repository.ListAsync(2, 2, ServiceAccountStatus.Active, "api", Arg.Any<CancellationToken>())
            .Returns((new List<ServiceAccount> { first, second }, 5));

        Result<PagedResult<ServiceAccountSummary>> result = await this.listHandler.HandleAsync(
            page: 2,
            pageSize: 2,
            status: ServiceAccountStatus.Active,
            searchTerm: "api");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(2);
        result.Value.TotalCount.ShouldBe(5);
        result.Value.TotalPages.ShouldBe(3);
        result.Value.HasNextPage.ShouldBeTrue();
        result.Value.HasPreviousPage.ShouldBeTrue();
        result.Value.Items[0].Id.ShouldBe(first.Id);
        result.Value.Items[0].ClientId.ShouldBe(first.ClientId);
        result.Value.Items[0].DisplayName.ShouldBe(first.DisplayName);
        result.Value.Items[0].Status.ShouldBe(ServiceAccountStatus.Active);
        result.Value.Items[0].AllowedScopes.ShouldBe(["api.read", "api.write"]);
        result.Value.Items[0].CredentialCount.ShouldBe(1);
        result.Value.Items[0].CertificateCount.ShouldBe(1);
        result.Value.Items[0].CreatedAt.ShouldBe(first.CreatedAt);
        result.Value.Items[0].ModifiedAt.ShouldBe(first.ModifiedAt);
        result.Value.Items[1].Status.ShouldBe(ServiceAccountStatus.Disabled);
    }

    [Fact]
    public async Task ListHandleAsync_ClampsPagingArgumentsAndPassesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        this.repository.ListAsync(1, 100, null, null, cts.Token)
            .Returns(([], 0));

        Result<PagedResult<ServiceAccountSummary>> result = await this.listHandler.HandleAsync(
            page: -10,
            pageSize: 500,
            cancellationToken: cts.Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Page.ShouldBe(1);
        result.Value.PageSize.ShouldBe(100);
        result.Value.Items.ShouldBeEmpty();
        await this.repository.Received(1).ListAsync(1, 100, null, null, cts.Token);
    }

    [Fact]
    public async Task GetHandleAsync_WhenServiceAccountExists_ReturnsDetails()
    {
        ServiceAccount serviceAccount = this.CreateServiceAccount("reports-api", "Reports API");
        serviceAccount.AddCredential("secret-hash", "primary secret", null, this.dateTimeProvider);
        serviceAccount.AddCertificate(
            "0123456789ABCDEF0123456789ABCDEF01234567",
            "CN=reports-api",
            this.now.AddYears(1),
            this.dateTimeProvider);
        this.repository.GetByIdAsync(serviceAccount.Id, Arg.Any<CancellationToken>())
            .Returns(serviceAccount);

        Result<ServiceAccountDetails> result = await this.getHandler.HandleAsync(serviceAccount.Id);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(serviceAccount.Id);
        result.Value.ClientId.ShouldBe("reports-api");
        result.Value.DisplayName.ShouldBe("Reports API");
        result.Value.Status.ShouldBe(ServiceAccountStatus.Active);
        result.Value.AllowedScopes.ShouldBe(["api.read", "api.write"]);
        result.Value.AllowedGrantTypes.ShouldBe(["client_credentials"]);
        result.Value.CredentialCount.ShouldBe(1);
        result.Value.CertificateCount.ShouldBe(1);
        result.Value.CreatedAt.ShouldBe(serviceAccount.CreatedAt);
        result.Value.ModifiedAt.ShouldBe(serviceAccount.ModifiedAt);
    }

    [Fact]
    public async Task GetHandleAsync_WhenServiceAccountDoesNotExist_ReturnsNotFound()
    {
        var serviceAccountId = ServiceAccountId.Create();
        this.repository.GetByIdAsync(serviceAccountId, Arg.Any<CancellationToken>())
            .Returns((ServiceAccount?)null);

        Result<ServiceAccountDetails> result = await this.getHandler.HandleAsync(serviceAccountId);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ServiceAccountErrors.NotFound.Code);
    }

    [Fact]
    public async Task GetHandleAsync_PassesCancellationToken()
    {
        var serviceAccountId = ServiceAccountId.Create();
        using var cts = new CancellationTokenSource();

        await this.getHandler.HandleAsync(serviceAccountId, cts.Token);

        await this.repository.Received(1).GetByIdAsync(serviceAccountId, cts.Token);
    }

    private ServiceAccount CreateServiceAccount(string clientId, string displayName)
    {
        return ServiceAccount.Create(
            clientId,
            displayName,
            ["api.read", "api.write"],
            ["client_credentials"],
            this.dateTimeProvider).Value;
    }
}
