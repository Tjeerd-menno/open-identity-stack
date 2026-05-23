using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenIdentityStack.Domain.ApplicationPermissions;

namespace OpenIdentityStack.Infrastructure.Persistence.ApplicationPermissions;

public sealed class DelegatedMaintainerConfiguration : IEntityTypeConfiguration<DelegatedMaintainer>
{
    public void Configure(EntityTypeBuilder<DelegatedMaintainer> builder)
    {
        builder.ToTable("DelegatedMaintainers");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasConversion(id => id.Value, value => new DelegatedMaintainerId(value))
            .ValueGeneratedNever();

        builder.Property(m => m.RegisteredApplicationId)
            .HasConversion(id => id.Value, value => new RegisteredApplicationId(value))
            .IsRequired();

        builder.Property(m => m.PrincipalId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(m => m.PrincipalType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(m => m.GrantedBy)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(m => m.GrantedAt).IsRequired();
        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.ModifiedAt);

        builder.HasIndex(m => new { m.RegisteredApplicationId, m.PrincipalId })
            .IsUnique()
            .HasDatabaseName("IX_DelegatedMaintainers_Application_Principal");
    }
}
