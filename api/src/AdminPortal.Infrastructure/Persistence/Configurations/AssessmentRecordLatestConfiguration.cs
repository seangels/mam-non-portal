using AdminPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdminPortal.Infrastructure.Persistence.Configurations;

internal sealed class AssessmentRecordLatestConfiguration : IEntityTypeConfiguration<AssessmentRecordLatest>
{
    public void Configure(EntityTypeBuilder<AssessmentRecordLatest> builder)
    {
        builder.ToTable("assessment_record_latests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.LatestGrade).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.Note).HasMaxLength(2000);
        builder.ComplexProperty(x => x.AssessmentSnapshot, nested => nested.ToJson());
        builder.HasIndex(x => new { x.AssessmentSheetLatestId, x.AssessmentId }).IsUnique();
        builder.HasOne(x => x.AssessmentSheetLatest).WithMany()
            .HasForeignKey(x => x.AssessmentSheetLatestId).OnDelete(DeleteBehavior.Restrict);
    }
}
