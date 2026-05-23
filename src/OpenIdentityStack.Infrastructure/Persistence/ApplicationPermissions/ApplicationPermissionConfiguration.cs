using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenIdentityStack.Domain.ApplicationPermissions;

namespace OpenIdentityStack.Infrastructure.Persistence.ApplicationPermissions;

public sealed class ApplicationPermissionConfiguration : IEntityTypeConfiguration<ApplicationPermission>
{
    public void Configure(EntityTypeBuilder<ApplicationPermission> builder)
    {
        builder.ToTable("ApplicationPermissions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, value => new ApplicationPermissionId(value))
            .ValueGeneratedNever();

        builder.Property(p => p.RegisteredApplicationId)
            .HasConversion(id => id.Value, value => new RegisteredApplicationId(value))
            .IsRequired();

        builder.Property(p => p.PermissionKey)
            .HasMaxLength(63)
            .IsRequired();

        builder.Property(p => p.FullPermissionKey)
            .HasMaxLength(127)
            .IsRequired();

        builder.Property(p => p.DisplayName)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasMaxLength(1000);

        builder.Property(p => p.Category)
            .HasMaxLength(1000);

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(p => p.IsAssignable)
            .IsRequired();

        builder.Property(p => p.CreatedBy)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(p => p.UpdatedBy)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.ModifiedAt);
        builder.Property(p => p.DisabledAt);
        builder.Property(p => p.DeprecatedAt);
        builder.Property(p => p.RetiredAt);

        builder.HasIndex(p => p.FullPermissionKey)
            .IsUnique()
            .HasDatabaseName("IX_ApplicationPermissions_FullPermissionKey");

        builder.HasIndex(p => new { p.RegisteredApplicationId, p.PermissionKey })
            .IsUnique()
            .HasDatabaseName("IX_ApplicationPermissions_ApplicationId_PermissionKey");

        builder.HasIndex(p => p.IsAssignable)
            .HasDatabaseName("IX_ApplicationPermissions_IsAssignable");

        builder.HasIndex(p => p.Status)
            .HasDatabaseName("IX_ApplicationPermissions_Status");
    }
}
