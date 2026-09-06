using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OpenIdentityStack.Infrastructure.Persistence;

public sealed class EmergencyAccessRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid SessionId { get; set; }
    public Guid Epoch { get; set; }
    public Guid? CredentialRevision { get; set; }
    public DateTimeOffset AuthenticatedAt { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

public sealed class ResourceWindowReviewRecord
{
    public Guid Id { get; set; }
    public Guid ResourceId { get; set; }
    public Guid Epoch { get; set; }
    public long ResourceRevision { get; set; }
    public string Mechanism { get; set; } = string.Empty;
    public int ResidualSeconds { get; set; }
    public string EvidenceReference { get; set; } = string.Empty;
    public DateTimeOffset ReviewedAt { get; set; }
}

public sealed class EmergencyAccessConfiguration : IEntityTypeConfiguration<EmergencyAccessRecord>
{
    public void Configure(EntityTypeBuilder<EmergencyAccessRecord> builder)
    {
        builder.ToTable("EmergencyAccessEvidence");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.Epoch, x.RecordedAt });
    }
}

public sealed class ResourceWindowReviewConfiguration : IEntityTypeConfiguration<ResourceWindowReviewRecord>
{
    public void Configure(EntityTypeBuilder<ResourceWindowReviewRecord> builder)
    {
        builder.ToTable("ResourceTokenWindowReviews");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Mechanism).HasMaxLength(64);
        builder.Property(x => x.EvidenceReference).HasMaxLength(1000);
        builder.HasIndex(x => new { x.ResourceId, x.Epoch, x.ReviewedAt });
    }
}
