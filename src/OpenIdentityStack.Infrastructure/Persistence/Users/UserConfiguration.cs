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

        builder.Property(u => u.GivenName)
            .HasMaxLength(256);

        builder.Property(u => u.FamilyName)
            .HasMaxLength(256);

        builder.Property(u => u.MiddleName)
            .HasMaxLength(256);

        builder.Property(u => u.Nickname)
            .HasMaxLength(256);

        builder.Property(u => u.PreferredUsername)
            .HasMaxLength(64);

        builder.Property(u => u.NormalizedPreferredUsername)
            .HasMaxLength(64);

        builder.Property(u => u.Profile)
            .HasMaxLength(2048);

        builder.Property(u => u.Picture)
            .HasMaxLength(2048);

        builder.Property(u => u.Website)
            .HasMaxLength(2048);

        builder.Property(u => u.Gender)
            .HasMaxLength(64);

        builder.Property(u => u.Birthdate)
            .HasMaxLength(32);

        builder.Property(u => u.ZoneInfo)
            .HasMaxLength(128);

        builder.Property(u => u.Locale)
            .HasMaxLength(35);

        // Flattened into the Users table: the address is part of the user's profile, never
        // queried on its own. All six columns null means the user simply has no address.
        builder.OwnsOne(u => u.Address, addressBuilder =>
        {
            addressBuilder.Property(a => a.Formatted)
                .HasColumnName("AddressFormatted")
                .HasMaxLength(512);

            addressBuilder.Property(a => a.StreetAddress)
                .HasColumnName("AddressStreetAddress")
                .HasMaxLength(256);

            addressBuilder.Property(a => a.Locality)
                .HasColumnName("AddressLocality")
                .HasMaxLength(256);

            addressBuilder.Property(a => a.Region)
                .HasColumnName("AddressRegion")
                .HasMaxLength(256);

            addressBuilder.Property(a => a.PostalCode)
                .HasColumnName("AddressPostalCode")
                .HasMaxLength(64);

            addressBuilder.Property(a => a.Country)
                .HasColumnName("AddressCountry")
                .HasMaxLength(256);
        });

        builder.Property(u => u.PhoneNumber)
            .HasMaxLength(64);

        builder.Property(u => u.PhoneNumberVerified)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(512);

        builder.Property(u => u.Status)
            .IsRequired()
            .IsConcurrencyToken()
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

        builder.HasIndex(u => u.NormalizedPreferredUsername)
            .IsUnique()
            .HasDatabaseName("IX_Users_NormalizedPreferredUsername");

        builder.Ignore(u => u.EmailVerified);
        builder.Navigation(u => u.EmailVerificationEvidence).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.OwnsMany(u => u.EmailVerificationEvidence, evidence =>
        {
            evidence.ToTable("UserEmailVerificationEvidence");
            evidence.WithOwner().HasForeignKey("UserId");
            evidence.HasKey(e => e.Id);
            evidence.Property(e => e.Id).ValueGeneratedNever();
            evidence.Property(e => e.NormalizedEmail).HasMaxLength(256).IsRequired();
            evidence.Ignore(e => e.ProviderId);
            evidence.Ignore(e => e.Issuer);
            evidence.Ignore(e => e.WithdrawnAt);
            evidence.HasIndex("UserId");
        });

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

            identityBuilder.Property(i => i.AssociationEvidence).HasConversion<string>().HasMaxLength(64);
            identityBuilder.Ignore(i => i.IsQuarantined);

            identityBuilder.Property(i => i.Issuer).HasMaxLength(2048);

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
