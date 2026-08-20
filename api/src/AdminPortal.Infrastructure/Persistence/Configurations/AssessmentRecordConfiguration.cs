using AdminPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdminPortal.Infrastructure.Persistence.Configurations;

internal sealed class AssessmentRecordConfiguration : IEntityTypeConfiguration<AssessmentRecord>
{
    public void Configure(EntityTypeBuilder<AssessmentRecord> builder)
    {
        builder.ToTable("assessment_records");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PlanGrade).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.FinalGrade).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.PlanNote).HasMaxLength(2000);
        builder.Property(x => x.FinalNote).HasMaxLength(2000);
        builder.ComplexProperty(x => x.AssessmentSnapshot, nested => nested.ToJson());
        builder.HasOne(x => x.AssessmentSheet).WithMany()
            .HasForeignKey(x => x.AssessmentSheetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UpdatedByUser).WithMany()
            .HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
