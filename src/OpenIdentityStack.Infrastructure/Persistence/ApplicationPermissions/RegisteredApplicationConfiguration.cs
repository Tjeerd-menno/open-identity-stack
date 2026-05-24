using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenIdentityStack.Domain.ApplicationPermissions;

namespace OpenIdentityStack.Infrastructure.Persistence.ApplicationPermissions;

public sealed class RegisteredApplicationConfiguration : IEntityTypeConfiguration<RegisteredApplication>
{
    public void Configure(EntityTypeBuilder<RegisteredApplication> builder)
    {
        builder.ToTable("RegisteredApplications");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, value => new RegisteredApplicationId(value))
            .ValueGeneratedNever();

        builder.Property(s => s.ApplicationIdentifier)
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
            .HasForeignKey(p => p.RegisteredApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Maintainers)
            .WithOne()
            .HasForeignKey(m => m.RegisteredApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Permissions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(s => s.Maintainers)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(s => s.ApplicationIdentifier)
            .IsUnique()
            .HasDatabaseName("IX_RegisteredApplications_ApplicationIdentifier");

        builder.HasIndex(s => s.OwnerId)
            .HasDatabaseName("IX_RegisteredApplications_OwnerId");

        builder.HasIndex(s => s.Status)
            .HasDatabaseName("IX_RegisteredApplications_Status");
    }
}
