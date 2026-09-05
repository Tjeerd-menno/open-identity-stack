using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OpenIdentityStack.Infrastructure.Persistence;

internal sealed class AdministrativeAuthorityRevision
{
    public int Id { get; set; } = 1;
    public long Revision { get; set; }
}

internal sealed class AdministrativeAuthorityRevisionConfiguration : IEntityTypeConfiguration<AdministrativeAuthorityRevision>
{
    public void Configure(EntityTypeBuilder<AdministrativeAuthorityRevision> builder)
    {
        builder.ToTable("AdministrativeAuthorityRevision");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).ValueGeneratedNever();
        builder.Property(value => value.Revision).IsConcurrencyToken();
        builder.HasData(new AdministrativeAuthorityRevision());
    }
}
