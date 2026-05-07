
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Roles;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Persistence.Roles;
/// <summary>
/// EF Core configuration for the RoleAssignment entity.
/// </summary>
internal sealed class RoleAssignmentConfiguration : IEntityTypeConfiguration<RoleAssignment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RoleAssignment> builder)
    {
        builder.ToTable("RoleAssignments");

        // Composite primary key
        builder.HasKey(ra => new { ra.UserId, ra.RoleId });

        builder.Property(ra => ra.UserId)
            .HasConversion(
                id => id.Value,
                value => new UserId(value))
            .IsRequired();

        builder.Property(ra => ra.RoleId)
            .HasConversion(
                id => id.Value,
                value => new RoleId(value))
            .IsRequired();

        builder.Property(ra => ra.AssignedAt)
            .IsRequired();

        // Indexes for efficient queries
        builder.HasIndex(ra => ra.UserId)
            .HasDatabaseName("IX_RoleAssignments_UserId");

        builder.HasIndex(ra => ra.RoleId)
            .HasDatabaseName("IX_RoleAssignments_RoleId");

        builder.HasIndex(ra => ra.AssignedAt)
            .HasDatabaseName("IX_RoleAssignments_AssignedAt");
    }
}
