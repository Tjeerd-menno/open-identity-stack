using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenIdentityStack.Domain.ServicePermissions;

namespace OpenIdentityStack.Infrastructure.Persistence.ServicePermissions;

public sealed class DelegatedMaintainerConfiguration : IEntityTypeConfiguration<DelegatedMaintainer>
{
    public void Configure(EntityTypeBuilder<DelegatedMaintainer> builder)
    {
        builder.ToTable("DelegatedMaintainers");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasConversion(id => id.Value, value => new DelegatedMaintainerId(value))
            .ValueGeneratedNever();

        builder.Property(m => m.RegisteredServiceId)
            .HasConversion(id => id.Value, value => new RegisteredServiceId(value))
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

        builder.HasIndex(m => new { m.RegisteredServiceId, m.PrincipalId })
            .IsUnique()
            .HasDatabaseName("IX_DelegatedMaintainers_Service_Principal");
    }
}
