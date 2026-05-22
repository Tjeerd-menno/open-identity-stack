using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenIdentityStack.Domain.ServicePermissions;

namespace OpenIdentityStack.Infrastructure.Persistence.ServicePermissions;

public sealed class ServicePermissionConfiguration : IEntityTypeConfiguration<ServicePermission>
{
    public void Configure(EntityTypeBuilder<ServicePermission> builder)
    {
        builder.ToTable("ServicePermissions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, value => new ServicePermissionId(value))
            .ValueGeneratedNever();

        builder.Property(p => p.RegisteredServiceId)
            .HasConversion(id => id.Value, value => new RegisteredServiceId(value))
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

        builder.Property(p => p.IntendedUse)
            .HasMaxLength(1000);

        builder.Property(p => p.DocumentationUrl)
            .HasMaxLength(2048);

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
            .HasDatabaseName("IX_ServicePermissions_FullPermissionKey");

        builder.HasIndex(p => new { p.RegisteredServiceId, p.PermissionKey })
            .IsUnique()
            .HasDatabaseName("IX_ServicePermissions_ServiceId_PermissionKey");

        builder.HasIndex(p => p.IsAssignable)
            .HasDatabaseName("IX_ServicePermissions_IsAssignable");

        builder.HasIndex(p => p.Status)
            .HasDatabaseName("IX_ServicePermissions_Status");
    }
}
