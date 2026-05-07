using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Persistence.Users;

/// <summary>
/// EF Core configuration for the User entity.
/// </summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasColumnName("Id")
            .IsRequired();

        builder.Property(u => u.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(u => u.NormalizedEmail)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(u => u.DisplayName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(512);

        builder.Property(u => u.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(u => u.MfaEnabled)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(u => u.MfaSecret)
            .HasMaxLength(512);

        builder.Property(u => u.LastLoginAt);

        builder.Property(u => u.CreatedAt)
            .IsRequired();

        // Indexes
        builder.HasIndex(u => u.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName("IX_Users_NormalizedEmail");

        builder.HasIndex(u => u.Status)
            .HasDatabaseName("IX_Users_Status");

        // Configure owned UpstreamIdentities collection using navigation property and backing field
        builder.Navigation(u => u.UpstreamIdentities)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(u => u.UpstreamIdentities, identityBuilder =>
        {
            identityBuilder.ToTable("UserUpstreamIdentities");

            identityBuilder.WithOwner()
                .HasForeignKey("UserId");

            identityBuilder.Property<int>("Id")
                .ValueGeneratedOnAdd();

            identityBuilder.HasKey("Id");

            identityBuilder.Property(i => i.ProviderId)
                .HasColumnName("ProviderId")
                .IsRequired();

            identityBuilder.Property(i => i.ProviderName)
                .HasColumnName("ProviderName")
                .HasMaxLength(256)
                .IsRequired();

            identityBuilder.Property(i => i.SubjectId)
                .HasColumnName("SubjectId")
                .HasMaxLength(512)
                .IsRequired();

            identityBuilder.Property(i => i.Email)
                .HasColumnName("Email")
                .HasMaxLength(256);

            identityBuilder.Property(i => i.LinkedAt)
                .HasColumnName("LinkedAt")
                .IsRequired();

            // Index for looking up users by upstream identity
            identityBuilder.HasIndex("ProviderId", "SubjectId")
                .IsUnique()
                .HasDatabaseName("IX_UserUpstreamIdentities_ProviderId_SubjectId");
        });
    }
}
