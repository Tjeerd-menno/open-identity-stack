using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenIdentityStack.Domain.ServicePermissions;

namespace OpenIdentityStack.Infrastructure.Persistence.ServicePermissions;

public sealed class RegisteredServiceConfiguration : IEntityTypeConfiguration<RegisteredService>
{
    public void Configure(EntityTypeBuilder<RegisteredService> builder)
    {
        builder.ToTable("RegisteredServices");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, value => new RegisteredServiceId(value))
            .ValueGeneratedNever();

        builder.Property(s => s.ServiceIdentifier)
            .HasMaxLength(63)
            .IsRequired();

        builder.Property(s => s.DisplayName)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(s => s.Description)
            .HasMaxLength(1000);

        builder.Property(s => s.OwnerId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(s => s.OwnerType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(s => s.CreatedBy)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(s => s.UpdatedBy)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.ModifiedAt);
        builder.Property(s => s.DisabledAt);
        builder.Property(s => s.RetiredAt);
        builder.Property(s => s.ConcurrencyToken).IsConcurrencyToken();

        builder.HasMany(s => s.Permissions)
            .WithOne()
            .HasForeignKey(p => p.RegisteredServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Maintainers)
            .WithOne()
            .HasForeignKey(m => m.RegisteredServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Permissions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(s => s.Maintainers)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(s => s.ServiceIdentifier)
            .IsUnique()
            .HasDatabaseName("IX_RegisteredServices_ServiceIdentifier");

        builder.HasIndex(s => s.OwnerId)
            .HasDatabaseName("IX_RegisteredServices_OwnerId");

        builder.HasIndex(s => s.Status)
            .HasDatabaseName("IX_RegisteredServices_Status");
    }
}
