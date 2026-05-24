using OpenIdentityStack.Application.Common;
using OpenIdentityStack.Domain.Applications;
using SharedKernel;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Application.Applications.Queries;

public sealed class ListApplicationsQueryHandler : IListApplicationsQueryHandler
{
    private readonly IApplicationRepository repository;

    public ListApplicationsQueryHandler(IApplicationRepository repository)
    {
        this.repository = repository;
    }

    public async Task<Result<PagedResult<ApplicationSummary>>> HandleAsync(
        int page = 1,
        int pageSize = 20,
        ApplicationType? type = null,
        ApplicationStatus? status = null,
        OAuthClientType? clientType = null,
        string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        (List<DomainApplication> items, int totalCount) = await this.repository.ListAsync(
            page,
            pageSize,
            type,
            status,
            clientType,
            searchTerm,
            cancellationToken);

        var summaries = items
            .Select(application => new ApplicationSummary(
                application.Id,
                application.ClientId,
                application.DisplayName,
                application.Type,
                application.ClientType,
                application.Status,
                application.RequiresMigrationReview,
                application.CreatedAt,
                application.ModifiedAt))
            .ToList();

        return PagedResult<ApplicationSummary>.Create(summaries, page, pageSize, totalCount);
    }
}

public sealed class GetApplicationQueryHandler : IGetApplicationQueryHandler
{
    private readonly IApplicationRepository repository;

    public GetApplicationQueryHandler(IApplicationRepository repository)
    {
        this.repository = repository;
    }

    public async Task<Result<ApplicationDetails>> HandleAsync(
        DomainApplicationId applicationId,
        CancellationToken cancellationToken = default)
    {
        DomainApplication? application = await this.repository.GetByIdAsync(applicationId, cancellationToken);
        if (application is null)
        {
            return ApplicationErrors.NotFound;
        }

        return new ApplicationDetails(
            application.Id,
            application.ClientId,
            application.DisplayName,
            application.Description,
            application.Type,
            application.ClientType,
            application.Status,
            application.AllowedGrantTypes,
            application.AllowedScopes,
            application.RedirectUris,
            application.PostLogoutRedirectUris,
            application.RequirePkce,
            application.RequireConsent,
            application.RequiresMigrationReview,
            application.MigrationSource,
            application.CreatedAt,
            application.ModifiedAt);
    }
}

public sealed class ListApplicationCredentialsQueryHandler : IListApplicationCredentialsQueryHandler
{
    private readonly IApplicationRepository repository;

    public ListApplicationCredentialsQueryHandler(IApplicationRepository repository)
    {
        this.repository = repository;
    }

    public async Task<Result<IReadOnlyList<ApplicationCredentialDetails>>> HandleAsync(
        ListApplicationCredentialsQuery query,
        CancellationToken cancellationToken = default)
    {
        DomainApplication? application = await this.repository.GetByIdAsync(query.ApplicationId, cancellationToken);
        if (application is null)
        {
            return ApplicationErrors.NotFound;
        }

        return application.Credentials
            .Select(credential => new ApplicationCredentialDetails(
                credential.Id,
                application.Id,
                credential.Type,
                credential.Thumbprint,
                credential.Subject,
                credential.Description,
                credential.ExpiresAt,
                credential.CreatedAt,
                credential.LastUsedAt,
                credential.RevokedAt))
            .ToList();
    }
}
