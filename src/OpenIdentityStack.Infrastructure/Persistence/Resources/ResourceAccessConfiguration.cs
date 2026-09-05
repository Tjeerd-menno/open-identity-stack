using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenIdentityStack.Domain.Resources;

namespace OpenIdentityStack.Infrastructure.Persistence.Resources;

public sealed class ProtectedResourceConfiguration : IEntityTypeConfiguration<ProtectedResource>
{
    public void Configure(EntityTypeBuilder<ProtectedResource> builder)
    {
        builder.ToTable("ProtectedResources");
        builder.HasKey(resource => resource.Id);
        builder.Property(resource => resource.Audience).HasMaxLength(2048).IsRequired();
        builder.Property(resource => resource.Scope).HasMaxLength(100).IsRequired();
        builder.Property(resource => resource.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(resource => resource.Revision).IsConcurrencyToken();
        builder.Property<List<string>>("permissionNamespaces").HasColumnName("PermissionNamespaces").HasColumnType("jsonb").IsRequired();
        builder.Ignore(resource => resource.PermissionNamespaces);
        builder.Ignore(resource => resource.IsAdministrative);
        builder.HasIndex(resource => resource.Audience).IsUnique();
        builder.HasIndex(resource => resource.Scope).IsUnique();
    }
}

public sealed class ClientResourceGrantConfiguration : IEntityTypeConfiguration<ClientResourceGrant>
{
    public void Configure(EntityTypeBuilder<ClientResourceGrant> builder)
    {
        builder.ToTable("ClientResourceGrants");
        builder.HasKey(grant => grant.Id);
        builder.Property(grant => grant.Revision).IsConcurrencyToken();
        builder.Property<List<string>>("delegatedPermissions").HasColumnName("DelegatedPermissions").HasColumnType("jsonb").IsRequired();
        builder.Property<List<string>>("applicationPermissions").HasColumnName("ApplicationPermissions").HasColumnType("jsonb").IsRequired();
        builder.Ignore(grant => grant.DelegatedPermissions);
        builder.Ignore(grant => grant.ApplicationPermissions);
        builder.HasIndex(grant => new { grant.ClientApplicationId, grant.ResourceId }).IsUnique();
        builder.HasOne<Domain.Applications.Application>().WithMany().HasForeignKey(grant => grant.ClientApplicationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProtectedResource>().WithMany().HasForeignKey(grant => grant.ResourceId).OnDelete(DeleteBehavior.Restrict);
    }
}
