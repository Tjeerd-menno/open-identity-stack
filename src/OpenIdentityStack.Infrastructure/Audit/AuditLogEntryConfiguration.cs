using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Audit;

/// <summary>
/// Entity Framework configuration for <see cref="AuditLogEntry"/>.
/// </summary>
internal sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("AuditLogEntries");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.UserId)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(entry => entry.Action)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(entry => entry.EntityType)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(entry => entry.EntityId)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(entry => entry.Details)
            .HasMaxLength(4000);

        builder.Property(entry => entry.BeforeState)
            .HasColumnType("text");

        builder.Property(entry => entry.AfterState)
            .HasColumnType("text");

        builder.Property(entry => entry.Timestamp)
            .IsRequired();

        builder.HasIndex(entry => entry.Timestamp);
        builder.HasIndex(entry => new { entry.EntityType, entry.EntityId });
        builder.HasIndex(entry => entry.UserId);
    }
}
