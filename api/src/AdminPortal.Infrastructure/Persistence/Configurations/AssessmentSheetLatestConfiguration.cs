using AdminPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdminPortal.Infrastructure.Persistence.Configurations;

internal sealed class AssessmentSheetLatestConfiguration : IEntityTypeConfiguration<AssessmentSheetLatest>
{
    public void Configure(EntityTypeBuilder<AssessmentSheetLatest> builder)
    {
        builder.ToTable("assessment_sheet_latests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(300).IsRequired();
        builder.Property(x => x.AssessmentSheetStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ResponsibleTeacherFullNameSnapshot).HasMaxLength(500);
        builder.Property(x => x.Note).HasMaxLength(2000);
        builder.Property(x => x.Feedback).HasMaxLength(2000);
        builder.ComplexProperty(x => x.StudentSnapshot, nested => nested.ToJson());
        builder.HasIndex(x => x.StudentId).IsUnique();
        builder.HasOne(x => x.Student).WithMany()
            .HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ResponsibleTeacher).WithMany()
            .HasForeignKey(x => x.ResponsibleTeacherId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UpdatedByUser).WithMany()
            .HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
