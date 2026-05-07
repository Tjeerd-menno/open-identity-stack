using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenIdentityStack.Domain.Common; // Added
using OpenIdentityStack.Domain.Groups;

namespace OpenIdentityStack.Infrastructure.Persistence.Groups;

internal sealed class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("Groups");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id)
            .HasConversion(id => id.Value, v => new GroupId(v));

        builder.Property(g => g.Name)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(g => g.Description)
            .HasMaxLength(512);
        
        builder.Property(g => g.ParentGroupId)
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                v => v.HasValue ? new GroupId(v.Value) : null);

        // Self-referencing relationship
        builder.HasOne(g => g.ParentGroup)
            .WithMany(g => g.ChildGroups)
            .HasForeignKey(g => g.ParentGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        // Memberships
        builder.HasMany(g => g.Memberships)
            .WithOne() // No navigation property in GroupMembership
            .HasForeignKey("GroupId") // Shadow FK or property? GroupMembership HAS GroupId property.
            .HasPrincipalKey(g => g.Id) // Explicitly say matching Id
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for common query patterns
        builder.HasIndex(g => g.Name)
            .IsUnique()
            .HasDatabaseName("IX_Groups_Name");

        builder.HasIndex(g => g.ParentGroupId)
            .HasDatabaseName("IX_Groups_ParentGroupId");

        // Mappings (Value Objects)
        builder.OwnsMany(g => g.Mappings, mb =>
        {
            mb.ToTable("GroupMappings");
            mb.WithOwner().HasForeignKey("GroupId");
            mb.Property<Guid>("Id");
            mb.HasKey("Id");

            mb.Property(m => m.Type).IsRequired();
            mb.Property(m => m.Target).HasMaxLength(256).IsRequired();
            mb.Property(m => m.Value).HasMaxLength(1024);
            mb.Property(m => m.TokenTarget).IsRequired();
        });
    }
}
