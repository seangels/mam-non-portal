using AdminPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdminPortal.Infrastructure.Persistence.Configurations;

internal sealed class AssessmentGroupConfiguration : IEntityTypeConfiguration<AssessmentGroup>
{
    public void Configure(EntityTypeBuilder<AssessmentGroup> builder)
    {
        builder.ToTable("assessment_groups");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IsRoot).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.Name).HasMaxLength(2000);
        builder.Property(x => x.Level).HasDefaultValue((short)7);
        builder.Property(x => x.ParentId);
    }
}
