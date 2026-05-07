using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenIdentityStack.Domain.ServiceAccounts;

namespace OpenIdentityStack.Infrastructure.Persistence.ServiceAccounts;

/// <summary>
/// EF Core configuration for the ClientCredential entity.
/// </summary>
public sealed class ClientCredentialConfiguration : IEntityTypeConfiguration<ClientCredential>
{
    public void Configure(EntityTypeBuilder<ClientCredential> builder)
    {
        builder.ToTable("ClientCredentials");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("Id")
            .IsRequired()
            .ValueGeneratedNever();

        builder.Property(c => c.ServiceAccountId)
            .IsRequired();

        builder.Property(c => c.SecretHash)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(c => c.Description)
            .HasMaxLength(256);

        builder.Property(c => c.ExpiresAt);

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.LastUsedAt);

        builder.Property(c => c.IsRevoked)
            .IsRequired()
            .HasDefaultValue(false);

        // Indexes
        builder.HasIndex(c => c.ServiceAccountId)
            .HasDatabaseName("IX_ClientCredentials_ServiceAccountId");
    }
}

/// <summary>
/// EF Core configuration for the ClientCertificate entity.
/// </summary>
public sealed class ClientCertificateConfiguration : IEntityTypeConfiguration<ClientCertificate>
{
    public void Configure(EntityTypeBuilder<ClientCertificate> builder)
    {
        builder.ToTable("ClientCertificates");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("Id")
            .IsRequired()
            .ValueGeneratedNever();

        builder.Property(c => c.ServiceAccountId)
            .IsRequired();

        builder.Property(c => c.Thumbprint)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(c => c.Subject)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(c => c.ExpiresAt)
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.IsRevoked)
            .IsRequired()
            .HasDefaultValue(false);

        // Indexes - thumbprint should be unique per service account
        builder.HasIndex(c => new { c.ServiceAccountId, c.Thumbprint })
            .IsUnique()
            .HasDatabaseName("IX_ClientCertificates_ServiceAccountId_Thumbprint");
    }
}
