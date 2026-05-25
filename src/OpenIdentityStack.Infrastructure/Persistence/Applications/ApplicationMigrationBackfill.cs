using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Domain.Applications;
using OpenIdentityStack.Domain.Clients;
using OpenIdentityStack.Domain.ServiceAccounts;
using SharedKernel;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;

namespace OpenIdentityStack.Infrastructure.Persistence.Applications;

public sealed class ApplicationMigrationBackfill
{
    private const string clientSource = "Client";
    private const string serviceAccountSource = "ServiceAccount";
    private readonly OpenIdentityStackDbContext dbContext;
    private readonly IDateTimeProvider dateTimeProvider;

    public ApplicationMigrationBackfill(OpenIdentityStackDbContext dbContext, IDateTimeProvider dateTimeProvider)
    {
        this.dbContext = dbContext;
        this.dateTimeProvider = dateTimeProvider;
    }

    public async Task<ApplicationMigrationBackfillResult> BackfillClientsAsync(CancellationToken cancellationToken = default)
    {
        List<Client> clients = await this.dbContext.Clients
            .AsNoTracking()
            .OrderBy(client => client.ClientIdValue)
            .ToListAsync(cancellationToken);
        HashSet<string> existingClientIds = await this.dbContext.Applications
            .AsNoTracking()
            .Select(application => application.ClientId)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken);

        int createdApplications = 0;
        foreach (Client client in clients)
        {
            if (existingClientIds.Contains(client.ClientIdValue))
            {
                continue;
            }

            DomainApplication application = CreateApplication(client);
            ApplicationMigrationClientProfile profile = ApplicationMigrationProfileInference.Infer(client);
            await this.dbContext.Applications.AddAsync(application, cancellationToken);
            this.dbContext.Entry(application).Property(item => item.RequiresMigrationReview).CurrentValue = profile.RequiresMigrationReview;
            this.dbContext.Entry(application).Property(item => item.MigrationSource).CurrentValue = clientSource;
            this.dbContext.Entry(application).Property(item => item.CreatedAt).CurrentValue = client.CreatedAt;
            this.dbContext.Entry(application).Property(item => item.ModifiedAt).CurrentValue = client.ModifiedAt;

            existingClientIds.Add(client.ClientIdValue);
            createdApplications++;
        }

        await this.dbContext.SaveChangesAsync(cancellationToken);
        return new ApplicationMigrationBackfillResult(createdApplications);
    }

    public async Task<ApplicationMigrationBackfillResult> BackfillServiceAccountsAsync(CancellationToken cancellationToken = default)
    {
        List<ServiceAccount> serviceAccounts = await this.dbContext.ServiceAccounts
            .AsNoTracking()
            .Include(serviceAccount => serviceAccount.Credentials)
            .Include(serviceAccount => serviceAccount.Certificates)
            .OrderBy(serviceAccount => serviceAccount.ClientId)
            .ToListAsync(cancellationToken);
        ServiceAccount? invalidGrantServiceAccount = serviceAccounts.FirstOrDefault(
            serviceAccount => serviceAccount.AllowedGrantTypes.Any(
                grantType => !string.Equals(grantType, "client_credentials", StringComparison.OrdinalIgnoreCase)));
        if (invalidGrantServiceAccount is not null)
        {
            throw new InvalidOperationException(
                $"Unable to migrate service account '{invalidGrantServiceAccount.ClientId}' because it contains grants other than client_credentials.");
        }

        HashSet<string> existingClientIds = await this.dbContext.Applications
            .AsNoTracking()
            .Select(application => application.ClientId)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken);

        int createdApplications = 0;
        foreach (ServiceAccount serviceAccount in serviceAccounts)
        {
            if (existingClientIds.Contains(serviceAccount.ClientId))
            {
                continue;
            }

            DomainApplication application = CreateServiceAccountApplication(serviceAccount);
            await this.dbContext.Applications.AddAsync(application, cancellationToken);
            this.dbContext.Entry(application).Property(item => item.RequiresMigrationReview).CurrentValue = false;
            this.dbContext.Entry(application).Property(item => item.MigrationSource).CurrentValue = serviceAccountSource;
            this.dbContext.Entry(application).Property(item => item.CreatedAt).CurrentValue = serviceAccount.CreatedAt;
            this.dbContext.Entry(application).Property(item => item.ModifiedAt).CurrentValue = serviceAccount.ModifiedAt;

            foreach (ClientCredential credential in serviceAccount.Credentials)
            {
                await this.AddCredentialAsync(application, credential, cancellationToken);
            }

            foreach (ClientCertificate certificate in serviceAccount.Certificates)
            {
                await this.AddCertificateAsync(application, certificate, cancellationToken);
            }

            existingClientIds.Add(serviceAccount.ClientId);
            createdApplications++;
        }

        await this.dbContext.SaveChangesAsync(cancellationToken);
        return new ApplicationMigrationBackfillResult(createdApplications);
    }

    private DomainApplication CreateApplication(Client client)
    {
        ApplicationMigrationClientProfile profile = ApplicationMigrationProfileInference.Infer(client);
        Result<DomainApplication> result = DomainApplication.Create(
            client.ClientIdValue,
            client.DisplayName,
            client.Description,
            profile.InferredProfile,
            MapClientType(client.ClientType),
            client.AllowedGrantTypes,
            client.AllowedScopes,
            client.RedirectUris,
            client.PostLogoutRedirectUris,
            client.RequirePkce,
            client.RequireConsent,
            this.dateTimeProvider);

        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"Unable to migrate legacy client '{client.ClientIdValue}' to an application: {result.Error.Code} {result.Error.Description}");
        }

        return result.Value;
    }

    private static OAuthClientType MapClientType(ClientType clientType) =>
        clientType == ClientType.Confidential
            ? OAuthClientType.Confidential
            : OAuthClientType.Public;

    private DomainApplication CreateServiceAccountApplication(ServiceAccount serviceAccount)
    {
        Result<DomainApplication> result = DomainApplication.CreateMachineToMachine(
            serviceAccount.ClientId,
            serviceAccount.DisplayName,
            description: null,
            serviceAccount.AllowedScopes,
            this.dateTimeProvider);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"Unable to migrate service account '{serviceAccount.ClientId}' to an application: {result.Error.Code} {result.Error.Description}");
        }

        DomainApplication application = result.Value;
        if (serviceAccount.Status == ServiceAccountStatus.Disabled)
        {
            Result disabled = application.Disable(this.dateTimeProvider);
            if (disabled.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Unable to disable migrated application for service account '{serviceAccount.ClientId}': {disabled.Error.Code} {disabled.Error.Description}");
            }
        }

        return application;
    }

    private async Task AddCredentialAsync(
        DomainApplication application,
        ClientCredential legacyCredential,
        CancellationToken cancellationToken)
    {
        Result<ApplicationCredential> result = ApplicationCredential.CreateClientSecret(
            application.Id,
            legacyCredential.SecretHash,
            legacyCredential.Description,
            legacyCredential.ExpiresAt,
            this.dateTimeProvider);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"Unable to migrate credential '{legacyCredential.Id}' for application '{application.ClientId}': {result.Error.Code} {result.Error.Description}");
        }

        ApplicationCredential credential = result.Value;
        await this.dbContext.ApplicationCredentials.AddAsync(credential, cancellationToken);
        this.dbContext.Entry(credential).Property(item => item.Id).CurrentValue = legacyCredential.Id;
        this.dbContext.Entry(credential).Property(item => item.CreatedAt).CurrentValue = legacyCredential.CreatedAt;
        this.dbContext.Entry(credential).Property(item => item.ModifiedAt).CurrentValue = legacyCredential.CreatedAt;
        this.dbContext.Entry(credential).Property(item => item.LastUsedAt).CurrentValue = legacyCredential.LastUsedAt;
        this.dbContext.Entry(credential).Property(item => item.RevokedAt).CurrentValue = legacyCredential.IsRevoked
            ? this.dateTimeProvider.UtcNow
            : null;
    }

    private async Task AddCertificateAsync(
        DomainApplication application,
        ClientCertificate legacyCertificate,
        CancellationToken cancellationToken)
    {
        Result<ApplicationCredential> result = ApplicationCredential.CreateCertificate(
            application.Id,
            legacyCertificate.Thumbprint,
            legacyCertificate.Subject,
            legacyCertificate.Subject,
            legacyCertificate.ExpiresAt,
            this.dateTimeProvider);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"Unable to migrate certificate '{legacyCertificate.Id}' for application '{application.ClientId}': {result.Error.Code} {result.Error.Description}");
        }

        ApplicationCredential certificate = result.Value;
        await this.dbContext.ApplicationCredentials.AddAsync(certificate, cancellationToken);
        this.dbContext.Entry(certificate).Property(item => item.Id).CurrentValue = legacyCertificate.Id;
        this.dbContext.Entry(certificate).Property(item => item.CreatedAt).CurrentValue = legacyCertificate.CreatedAt;
        this.dbContext.Entry(certificate).Property(item => item.ModifiedAt).CurrentValue = legacyCertificate.CreatedAt;
        this.dbContext.Entry(certificate).Property(item => item.RevokedAt).CurrentValue = legacyCertificate.IsRevoked
            ? this.dateTimeProvider.UtcNow
            : null;
    }
}

public sealed record ApplicationMigrationBackfillResult(int CreatedApplications);
