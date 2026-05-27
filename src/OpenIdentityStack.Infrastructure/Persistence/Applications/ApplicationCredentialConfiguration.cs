using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenIdentityStack.Domain.Applications;

namespace OpenIdentityStack.Infrastructure.Persistence.Applications;

public sealed class ApplicationCredentialConfiguration : IEntityTypeConfiguration<ApplicationCredential>
{
    public void Configure(EntityTypeBuilder<ApplicationCredential> builder)
    {
        builder.ToTable("ApplicationCredentials");

        builder.HasKey(credential => credential.Id);

        builder.Property(credential => credential.Id)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(credential => credential.ApplicationId)
            .IsRequired();

        builder.Property(credential => credential.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(credential => credential.SecretHash)
            .HasMaxLength(512);

        builder.Property(credential => credential.Thumbprint)
            .HasMaxLength(128);

        builder.Property(credential => credential.Subject)
            .HasMaxLength(512);

        builder.Property(credential => credential.Description)
            .HasMaxLength(1000);

        builder.Property(credential => credential.ExpiresAt);

        builder.Property(credential => credential.CreatedAt)
            .IsRequired();

        builder.Property(credential => credential.LastUsedAt);

        builder.Property(credential => credential.RevokedAt);

        builder.Property(credential => credential.ModifiedAt);

        builder.HasIndex(credential => credential.ApplicationId)
            .HasDatabaseName("IX_ApplicationCredentials_ApplicationId");

        builder.HasIndex(credential => new
            {
                credential.ApplicationId,
                credential.Type,
                credential.RevokedAt
            })
            .HasDatabaseName("IX_ApplicationCredentials_ApplicationId_Type_RevokedAt");

        builder.HasIndex(credential => new
            {
                credential.ApplicationId,
                credential.Thumbprint
            })
            .IsUnique()
            .HasFilter("\"Thumbprint\" IS NOT NULL AND \"RevokedAt\" IS NULL")
            .HasDatabaseName("IX_ApplicationCredentials_ApplicationId_Thumbprint");
    }
}
