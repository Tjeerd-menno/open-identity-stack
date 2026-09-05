using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OpenIdentityStack.Domain.Federation;

namespace OpenIdentityStack.Infrastructure.Persistence.Federation;

/// <summary>
/// EF Core configuration for UpstreamProvider entity.
/// </summary>
public sealed class UpstreamProviderConfiguration : IEntityTypeConfiguration<UpstreamProvider>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UpstreamProvider> builder)
    {
        builder.ToTable("upstream_providers");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id")
            .HasConversion(
                id => id.Value,
                value => UpstreamProviderId.From(value));

        builder.Property(p => p.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(p => p.Name)
            .IsUnique()
            .HasDatabaseName("IX_UpstreamProviders_Name");

        builder.HasIndex(p => p.Status)
            .HasDatabaseName("IX_UpstreamProviders_Status");

        builder.Property(p => p.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(p => p.Authority)
            .HasColumnName("authority")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(p => p.IdentityConfigurationLocked).HasColumnName("identity_configuration_locked");
        builder.Property(p => p.BoundIssuer).HasColumnName("bound_issuer").HasMaxLength(2048);
        builder.Property(p => p.IdentityVersion).HasColumnName("identity_version").IsConcurrencyToken();

        builder.Property(p => p.ClientId)
            .HasColumnName("client_id")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(p => p.TrustEmailVerification).HasColumnName("trust_email_verification").HasDefaultValue(false);
        builder.Property(p => p.EmailTrustVersion).HasColumnName("email_trust_version").IsConcurrencyToken();

        builder.Property(p => p.ClientSecret)
            .HasColumnName("client_secret")
            .HasMaxLength(500);

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(p => p.JitProvisioningEnabled)
            .IsConcurrencyToken()
            .HasColumnName("jit_provisioning_enabled")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Configure ClaimMappings as owned entities
        builder.OwnsMany(p => p.ClaimMappings, cm =>
        {
            cm.ToTable("upstream_provider_claim_mappings");

            cm.WithOwner().HasForeignKey("upstream_provider_id");

            cm.Property<int>("id")
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            cm.HasKey("id");

            cm.Property(m => m.SourceClaim)
                .HasColumnName("source_claim")
                .HasMaxLength(256)
                .IsRequired();

            cm.Property(m => m.TargetClaim)
                .HasColumnName("target_claim")
                .HasMaxLength(256)
                .IsRequired();

            cm.Property(m => m.TransformType)
                .HasColumnName("transform_type")
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            cm.Property(m => m.TransformPattern)
                .HasColumnName("transform_pattern")
                .HasMaxLength(500);

            cm.HasIndex("upstream_provider_id", "SourceClaim")
                .IsUnique();
        });
    }
}
