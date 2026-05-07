using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenIdentityStack.Domain.Clients;

namespace OpenIdentityStack.Infrastructure.Persistence.Clients;

/// <summary>
/// EF Core configuration for the Client entity.
/// </summary>
public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Clients");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("Id")
            .IsRequired();

        builder.Property(c => c.ClientIdValue)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(c => c.DisplayName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(c => c.ClientType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(c => c.Description)
            .HasMaxLength(1000);

        // Store collections as JSON arrays
        builder.Property(c => c.RedirectUris)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(c => c.PostLogoutRedirectUris)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(c => c.AllowedScopes)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(c => c.AllowedGrantTypes)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(c => c.RequirePkce)
            .IsRequired();

        builder.Property(c => c.RequireConsent)
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.ModifiedAt);

        // Indexes
        builder.HasIndex(c => c.ClientIdValue)
            .IsUnique()
            .HasDatabaseName("IX_Clients_ClientIdValue");

        builder.HasIndex(c => c.CreatedAt)
            .HasDatabaseName("IX_Clients_CreatedAt");
    }
}
