using AdminPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdminPortal.Infrastructure.Persistence.Configurations;

internal sealed class AssessmentConfiguration : IEntityTypeConfiguration<Assessment>
{
    public void Configure(EntityTypeBuilder<Assessment> builder)
    {
        builder.ToTable("assessments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.GroupLv1Name).HasMaxLength(500);
        builder.Property(x => x.GroupLv2Name).HasMaxLength(500);
        builder.Property(x => x.GroupLv3Name).HasMaxLength(500);
        builder.Property(x => x.Note).HasMaxLength(2000);
        builder.Property(x => x.RowIndex);
        
    }
}
