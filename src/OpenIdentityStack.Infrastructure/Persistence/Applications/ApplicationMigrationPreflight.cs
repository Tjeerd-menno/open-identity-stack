using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Domain.Applications;
using OpenIdentityStack.Domain.Clients;
using OpenIdentityStack.Domain.ServiceAccounts;

namespace OpenIdentityStack.Infrastructure.Persistence.Applications;

public sealed class ApplicationMigrationPreflight
{
    private const string clientSource = "Client";
    private const string serviceAccountSource = "ServiceAccount";
    private readonly OpenIdentityStackDbContext dbContext;

    public ApplicationMigrationPreflight(OpenIdentityStackDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<ApplicationMigrationPreflightReport> AnalyzeAsync(CancellationToken cancellationToken = default)
    {
        List<Client> clients = await this.dbContext.Clients.AsNoTracking().ToListAsync(cancellationToken);
        List<ServiceAccount> serviceAccounts = await this.dbContext.ServiceAccounts.AsNoTracking().ToListAsync(cancellationToken);

        var blockingIssues = new List<ApplicationMigrationPreflightIssue>();
        var reviewItems = new List<ApplicationMigrationReviewItem>();

        blockingIssues.AddRange(FindDuplicateClientIds(clients, serviceAccounts));
        blockingIssues.AddRange(FindInvalidServiceAccountGrants(serviceAccounts));
        reviewItems.AddRange(FindClientReviewItems(clients));

        return new ApplicationMigrationPreflightReport(blockingIssues, reviewItems);
    }

    private static IEnumerable<ApplicationMigrationPreflightIssue> FindDuplicateClientIds(
        IReadOnlyList<Client> clients,
        IReadOnlyList<ServiceAccount> serviceAccounts)
    {
        return clients
            .Select(client => new MigrationClientIdentity(client.ClientIdValue, clientSource))
            .Concat(serviceAccounts.Select(serviceAccount => new MigrationClientIdentity(serviceAccount.ClientId, serviceAccountSource)))
            .GroupBy(identity => identity.ClientId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => new ApplicationMigrationPreflightIssue(
                ApplicationMigrationPreflightIssueCodes.DuplicateClientId,
                group.Key,
                group.Select(identity => identity.Source).Distinct(StringComparer.Ordinal).Order().ToList(),
                "Client ID appears in multiple legacy registration sources."));
    }

    private static IEnumerable<ApplicationMigrationPreflightIssue> FindInvalidServiceAccountGrants(
        IReadOnlyList<ServiceAccount> serviceAccounts)
    {
        return serviceAccounts
            .Select(serviceAccount => new
            {
                ServiceAccount = serviceAccount,
                InvalidGrants = serviceAccount.AllowedGrantTypes
                    .Where(grantType => !string.Equals(grantType, "client_credentials", StringComparison.OrdinalIgnoreCase))
                    .ToList()
            })
            .Where(item => item.InvalidGrants.Count > 0)
            .Select(item => new ApplicationMigrationPreflightIssue(
                ApplicationMigrationPreflightIssueCodes.InvalidServiceAccountGrant,
                item.ServiceAccount.ClientId,
                [serviceAccountSource],
                $"Service account contains unsupported grants: {string.Join(", ", item.InvalidGrants)}."));
    }

    private static IEnumerable<ApplicationMigrationReviewItem> FindClientReviewItems(IReadOnlyList<Client> clients)
    {
        return clients
            .Select(client =>
            {
                ApplicationMigrationClientProfile profile = ApplicationMigrationProfileInference.Infer(client);
                return new ApplicationMigrationReviewItem(
                    client.ClientIdValue,
                    clientSource,
                    profile.InferredType,
                    profile.RequiresMigrationReview);
            })
            .Where(item => item.RequiresMigrationReview);
    }

    private sealed record MigrationClientIdentity(string ClientId, string Source);
}

public sealed record ApplicationMigrationPreflightReport(
    IReadOnlyList<ApplicationMigrationPreflightIssue> BlockingIssues,
    IReadOnlyList<ApplicationMigrationReviewItem> ReviewItems)
{
    public bool CanMigrate => this.BlockingIssues.Count == 0;
}

public sealed record ApplicationMigrationPreflightIssue(
    string Code,
    string ClientId,
    IReadOnlyList<string> Sources,
    string Details);

public sealed record ApplicationMigrationReviewItem(
    string ClientId,
    string Source,
    ApplicationType InferredType,
    bool RequiresMigrationReview);

public static class ApplicationMigrationPreflightIssueCodes
{
    public const string DuplicateClientId = "ApplicationMigration.DuplicateClientId";
    public const string InvalidServiceAccountGrant = "ApplicationMigration.InvalidServiceAccountGrant";
}
