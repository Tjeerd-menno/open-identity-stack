using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata;
using OpenIdentityStack.Domain.Applications;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;

namespace OpenIdentityStack.Infrastructure.Persistence.Applications;

/// <summary>
/// EF Core configuration shell for the Application aggregate.
/// </summary>
public sealed class ApplicationConfiguration : IEntityTypeConfiguration<DomainApplication>
{
    public void Configure(EntityTypeBuilder<DomainApplication> builder)
    {
        builder.ToTable("Applications");

        builder.HasKey(application => application.Id);

        builder.Property(application => application.Id)
            .HasColumnName("Id")
            .IsRequired();

        builder.Property(application => application.ClientId)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(application => application.DisplayName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(application => application.Description)
            .HasMaxLength(1000);

        builder.Property(application => application.Profile)
            .HasColumnName("Profile")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(application => application.ClientType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(application => application.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property<List<string>>("allowedGrantTypes")
            .HasColumnName("AllowedGrantTypes")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property<List<string>>("allowedScopes")
            .HasColumnName("AllowedScopes")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property<List<string>>("redirectUris")
            .HasColumnName("RedirectUris")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property<List<string>>("postLogoutRedirectUris")
            .HasColumnName("PostLogoutRedirectUris")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(application => application.RequirePkce)
            .IsRequired();

        builder.Property(application => application.RequireConsent)
            .IsRequired();

        builder.Property(application => application.RequiresMigrationReview)
            .IsRequired();

        builder.Property(application => application.MigrationSource)
            .HasMaxLength(64);

        builder.Property(application => application.CreatedAt)
            .IsRequired();

        builder.Property(application => application.ModifiedAt);

        builder.HasIndex(application => application.ClientId)
            .IsUnique()
            .HasDatabaseName("IX_Applications_ClientId");

        builder.HasIndex(application => application.Status)
            .HasDatabaseName("IX_Applications_Status");

        builder.HasIndex(application => application.Profile)
            .HasDatabaseName("IX_Applications_Profile");

        builder.HasMany(application => application.Credentials)
            .WithOne()
            .HasForeignKey(credential => credential.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(application => application.Credentials)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
