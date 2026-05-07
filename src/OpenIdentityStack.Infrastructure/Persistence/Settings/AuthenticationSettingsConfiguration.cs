using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenIdentityStack.Domain.Settings;

namespace OpenIdentityStack.Infrastructure.Persistence.Settings;

/// <summary>
/// EF Core configuration for the AuthenticationSettings entity.
/// </summary>
internal sealed class AuthenticationSettingsConfiguration : IEntityTypeConfiguration<AuthenticationSettings>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuthenticationSettings> builder)
    {
        builder.ToTable("AuthenticationSettings");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasConversion(
                id => id.Value,
                value => new AuthenticationSettingsId(value))
            .ValueGeneratedNever();

        builder.Property(s => s.DefaultProviderId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.LocalFallbackEnabled)
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .IsRequired();
    }
}
