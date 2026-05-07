using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Groups;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Persistence.Groups;

internal sealed class GroupMembershipConfiguration : IEntityTypeConfiguration<GroupMembership>
{
    public void Configure(EntityTypeBuilder<GroupMembership> builder)
    {
        builder.ToTable("GroupMemberships");

        builder.HasKey(m => new { m.GroupId, m.UserId });

        builder.Property(m => m.GroupId)
            .HasConversion(id => id.Value, v => new GroupId(v));

        builder.Property(m => m.UserId)
            .HasConversion(id => id.Value, v => new UserId(v));

        builder.Property(m => m.AssignedBy)
            .HasConversion(id => id.Value, v => new UserId(v));
        
        builder.Property(m => m.AssignedAt)
            .IsRequired();

        // Indexes for common query patterns
        builder.HasIndex(m => m.UserId)
            .HasDatabaseName("IX_GroupMemberships_UserId");

        builder.HasIndex(m => m.GroupId)
            .HasDatabaseName("IX_GroupMemberships_GroupId");

        builder.HasIndex(m => m.AssignedAt)
            .HasDatabaseName("IX_GroupMemberships_AssignedAt");
    }
}
