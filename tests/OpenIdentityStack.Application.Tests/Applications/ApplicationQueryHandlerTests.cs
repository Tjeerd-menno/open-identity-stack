using OpenIdentityStack.Application.Applications;
using OpenIdentityStack.Application.Applications.Queries;
using OpenIdentityStack.Application.Common;
using OpenIdentityStack.Domain.Applications;
using SharedKernel;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Application.Tests.Applications;

public sealed class ApplicationQueryHandlerTests
{
    private readonly IApplicationRepository repository;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly ListApplicationsQueryHandler listHandler;
    private readonly GetApplicationQueryHandler getHandler;
    private readonly DateTimeOffset now = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);

    public ApplicationQueryHandlerTests()
    {
        this.repository = Substitute.For<IApplicationRepository>();
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.dateTimeProvider.UtcNow.Returns(this.now);
        this.listHandler = new ListApplicationsQueryHandler(this.repository);
        this.getHandler = new GetApplicationQueryHandler(this.repository);
    }

    [Fact]
    public async Task ListHandleAsync_ReturnsMappedSummariesAndPaginationMetadata()
    {
        DomainApplication first = this.CreateApplication("orders-web", "Orders Web");
        DomainApplication second = this.CreateApplication("billing-m2m", "Billing M2M", ApplicationType.MachineToMachine);
        second.Disable(this.dateTimeProvider);
        this.repository.ListAsync(2, 2, ApplicationType.Web, ApplicationStatus.Active, OAuthClientType.Confidential, "web", Arg.Any<CancellationToken>())
            .Returns((new List<DomainApplication> { first, second }, 5));

        Result<PagedResult<ApplicationSummary>> result = await this.listHandler.HandleAsync(
            page: 2,
            pageSize: 2,
            type: ApplicationType.Web,
            status: ApplicationStatus.Active,
            clientType: OAuthClientType.Confidential,
            searchTerm: "web");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(2);
        result.Value.TotalCount.ShouldBe(5);
        result.Value.TotalPages.ShouldBe(3);
        result.Value.HasNextPage.ShouldBeTrue();
        result.Value.Items[0].Id.ShouldBe(first.Id);
        result.Value.Items[0].ClientId.ShouldBe("orders-web");
        result.Value.Items[0].DisplayName.ShouldBe("Orders Web");
        result.Value.Items[0].Type.ShouldBe(ApplicationType.Web);
        result.Value.Items[1].Status.ShouldBe(ApplicationStatus.Disabled);
    }

    [Fact]
    public async Task ListHandleAsync_ClampsPagingArgumentsAndPassesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        this.repository.ListAsync(1, 100, null, null, null, null, cts.Token)
            .Returns(([], 0));

        Result<PagedResult<ApplicationSummary>> result = await this.listHandler.HandleAsync(
            page: -10,
            pageSize: 500,
            cancellationToken: cts.Token);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Page.ShouldBe(1);
        result.Value.PageSize.ShouldBe(100);
        await this.repository.Received(1).ListAsync(1, 100, null, null, null, null, cts.Token);
    }

    [Fact]
    public async Task GetHandleAsync_WhenApplicationExists_ReturnsDetails()
    {
        DomainApplication application = this.CreateApplication("orders-web", "Orders Web");
        this.repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>())
            .Returns(application);

        Result<ApplicationDetails> result = await this.getHandler.HandleAsync(application.Id);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(application.Id);
        result.Value.ClientId.ShouldBe("orders-web");
        result.Value.DisplayName.ShouldBe("Orders Web");
        result.Value.Type.ShouldBe(ApplicationType.Web);
        result.Value.ClientType.ShouldBe(OAuthClientType.Confidential);
        result.Value.AllowedGrantTypes.ShouldBe(["authorization_code"]);
        result.Value.RedirectUris.ShouldBe(["https://orders-web.example.com/callback"]);
    }

    [Fact]
    public async Task GetHandleAsync_WhenApplicationDoesNotExist_ReturnsNotFound()
    {
        var applicationId = DomainApplicationId.Create();
        this.repository.GetByIdAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns((DomainApplication?)null);

        Result<ApplicationDetails> result = await this.getHandler.HandleAsync(applicationId);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ApplicationErrors.NotFound.Code);
    }

    private DomainApplication CreateApplication(
        string clientId,
        string displayName,
        ApplicationType type = ApplicationType.Web) =>
        DomainApplication.Create(
            clientId,
            displayName,
            null,
            type,
            type == ApplicationType.MachineToMachine ? OAuthClientType.Confidential : OAuthClientType.Confidential,
            type == ApplicationType.MachineToMachine ? ["client_credentials"] : ["authorization_code"],
            ["openid"],
            type == ApplicationType.MachineToMachine ? [] : [$"https://{clientId}.example.com/callback"],
            [],
            requirePkce: false,
            requireConsent: type != ApplicationType.MachineToMachine,
            this.dateTimeProvider).Value;
}
