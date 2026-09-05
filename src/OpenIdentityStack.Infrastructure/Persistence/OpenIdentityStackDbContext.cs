using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.Globalization;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Domain.Groups; // Added
using OpenIdentityStack.Domain.ApplicationPermissions;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Domain.Settings;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Infrastructure.Audit;
using OpenIdentityStack.Infrastructure.Persistence.Converters;

using SharedKernel;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;
using DomainApplicationCredential = OpenIdentityStack.Domain.Applications.ApplicationCredential;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;
namespace OpenIdentityStack.Infrastructure.Persistence;

/// <summary>
/// Main database context for the OpenIdentityStack.
/// Configured to use OpenIddict entity stores for OIDC/OAuth2 entities.
/// </summary>
public class OpenIdentityStackDbContext : DbContext, IDataProtectionKeyContext
{
    public OpenIdentityStackDbContext(DbContextOptions<OpenIdentityStackDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the ASP.NET Core Data Protection keys.
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    /// <summary>
    /// Gets or sets the Users DbSet.
    /// </summary>
    public DbSet<User> Users => this.Set<User>();

    /// <summary>
    /// Gets or sets the UpstreamProviders DbSet.
    /// </summary>
    public DbSet<UpstreamProvider> UpstreamProviders => this.Set<UpstreamProvider>();

    /// <summary>
    /// Gets or sets the Roles DbSet.
    /// </summary>
    public DbSet<Role> Roles => this.Set<Role>();

    /// <summary>
    /// Gets or sets the RoleAssignments DbSet.
    /// </summary>
    public DbSet<RoleAssignment> RoleAssignments => this.Set<RoleAssignment>();

    /// <summary>
    /// Gets or sets the Groups DbSet.
    /// </summary>
    public DbSet<Group> Groups => this.Set<Group>();

    /// <summary>
    /// Gets or sets the UserSessions DbSet.
    /// </summary>
    public DbSet<UserSession> UserSessions => this.Set<UserSession>();

    /// <summary>
    /// Gets or sets the Applications DbSet.
    /// </summary>
    public DbSet<DomainApplication> Applications => this.Set<DomainApplication>();

    /// <summary>
    /// Gets or sets the ApplicationCredentials DbSet.
    /// </summary>
    public DbSet<DomainApplicationCredential> ApplicationCredentials => this.Set<DomainApplicationCredential>();

    /// <summary>
    /// Gets or sets the AuthenticationSettings DbSet.
    /// </summary>
    public DbSet<AuthenticationSettings> AuthenticationSettings => this.Set<AuthenticationSettings>();

    /// Gets or sets the persisted audit log entries DbSet.
    /// </summary>
    public DbSet<AuditLogEntry> AuditLogEntries => this.Set<AuditLogEntry>();
    /// <summary>
    /// Gets or sets the RegisteredApplications DbSet.
    /// </summary>
    public DbSet<RegisteredApplication> RegisteredApplications => this.Set<RegisteredApplication>();

    /// <summary>
    /// Gets or sets the ApplicationPermissions DbSet.
    /// </summary>
    public DbSet<ApplicationPermission> ApplicationPermissions => this.Set<ApplicationPermission>();

    /// <summary>
    /// Gets or sets the DelegatedMaintainers DbSet.
    /// </summary>
    public DbSet<DelegatedMaintainer> DelegatedMaintainers => this.Set<DelegatedMaintainer>();

    /// <inheritdoc />
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        foreach (UpstreamProviderId providerId in this.GetAddedIdentityProviderIds())
        {
            this.UpstreamProviders.Find(providerId)?.LockIdentityConfiguration();
        }
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <inheritdoc />
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        await this.LockLinkedProvidersAsync(cancellationToken);
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private async Task LockLinkedProvidersAsync(CancellationToken cancellationToken)
    {
        // Every insertion, including legacy/raw domain links, locks the provider in the same save.
        // Updating the concurrency token prevents a stale authority edit from racing the first link.
        foreach (UpstreamProviderId providerId in this.GetAddedIdentityProviderIds())
        {
            UpstreamProvider? provider = await this.UpstreamProviders.FindAsync([providerId], cancellationToken);
            provider?.LockIdentityConfiguration();
        }
    }

    private UpstreamProviderId[] GetAddedIdentityProviderIds() => this.ChangeTracker.Entries<UpstreamIdentity>()
        .Where(entry => entry.State == EntityState.Added)
        .Select(entry => entry.Entity.ProviderId).Distinct().ToArray();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Ignore domain event types - they are not persisted entities
        modelBuilder.Ignore<DomainEvent>();

        if (string.Equals(Database.ProviderName, "Microsoft.EntityFrameworkCore.Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var dateTimeOffsetConverter = new ValueConverter<DateTimeOffset, string>(
                v => v.ToString("o"),
                v => DateTimeOffset.Parse(v, CultureInfo.InvariantCulture));
            var nullableDateTimeOffsetConverter = new ValueConverter<DateTimeOffset?, string?>(
                v => v.HasValue ? v.Value.ToString("o") : null,
                v => v == null ? null : DateTimeOffset.Parse(v, CultureInfo.InvariantCulture));

            foreach (Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (Microsoft.EntityFrameworkCore.Metadata.IMutableProperty property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTimeOffset))
                    {
                        property.SetValueConverter(dateTimeOffsetConverter);
                    }
                    else if (property.ClrType == typeof(DateTimeOffset?))
                    {
                        property.SetValueConverter(nullableDateTimeOffsetConverter);
                    }
                }
            }
        }

        // Apply all configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OpenIdentityStackDbContext).Assembly);

        // OpenIddict entity sets are configured via UseOpenIddict() in OnConfiguring or AddDbContext
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Configure strongly-typed ID conversions
        configurationBuilder
            .Properties<UserId>()
            .HaveConversion<UserIdConverter>();

        configurationBuilder
            .Properties<RoleId>()
            .HaveConversion<RoleIdConverter>();

        configurationBuilder
            .Properties<GroupId>()
            .HaveConversion<GroupIdConverter>();

        configurationBuilder
            .Properties<SessionId>()
            .HaveConversion<SessionIdConverter>();

        configurationBuilder
            .Properties<ProviderId>()
            .HaveConversion<ProviderIdConverter>();

        configurationBuilder
            .Properties<UpstreamProviderId>()
            .HaveConversion<UpstreamProviderIdConverter>();

        configurationBuilder
            .Properties<DomainApplicationId>()
            .HaveConversion<ApplicationIdConverter>();

        configurationBuilder
            .Properties<AuthenticationSettingsId>()
            .HaveConversion<AuthenticationSettingsIdConverter>();

        configurationBuilder
            .Properties<RegisteredApplicationId>()
            .HaveConversion<RegisteredApplicationIdConverter>();

        configurationBuilder
            .Properties<ApplicationPermissionId>()
            .HaveConversion<ApplicationPermissionIdConverter>();

        configurationBuilder
            .Properties<DelegatedMaintainerId>()
            .HaveConversion<DelegatedMaintainerIdConverter>();
    }
}
