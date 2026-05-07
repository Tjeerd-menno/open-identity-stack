using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenIdentityStack.Domain.ServiceAccounts;

namespace OpenIdentityStack.Infrastructure.Persistence.ServiceAccounts;

/// <summary>
/// EF Core configuration for the ServiceAccount entity.
/// </summary>
public sealed class ServiceAccountConfiguration : IEntityTypeConfiguration<ServiceAccount>
{
    public void Configure(EntityTypeBuilder<ServiceAccount> builder)
    {
        builder.ToTable("ServiceAccounts");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("Id")
            .IsRequired();

        builder.Property(s => s.ClientId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(s => s.DisplayName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<int>();

        // Store AllowedScopes as JSON array
        builder.Property(s => s.AllowedScopes)
            .HasColumnType("jsonb")
            .IsRequired();

        // Store AllowedGrantTypes as JSON array
        builder.Property(s => s.AllowedGrantTypes)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.ModifiedAt);

        // Configure one-to-many relationship with ClientCredential
        builder.HasMany(s => s.Credentials)
            .WithOne()
            .HasForeignKey(c => c.ServiceAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure one-to-many relationship with ClientCertificate
        builder.HasMany(s => s.Certificates)
            .WithOne()
            .HasForeignKey(c => c.ServiceAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(s => s.ClientId)
            .IsUnique()
            .HasDatabaseName("IX_ServiceAccounts_ClientId");

        builder.HasIndex(s => s.Status)
            .HasDatabaseName("IX_ServiceAccounts_Status");
    }
}
