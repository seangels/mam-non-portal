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
        builder.Property(x => x.Name).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Note).HasMaxLength(2000);
        builder.Property(x => x.RowIndex);
        builder.HasOne(x => x.GroupLv1)
            .WithMany(x => x.AssessmentsLv1)
            .HasForeignKey(x => x.GroupLv1Id)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.GroupLv2)
            .WithMany(x => x.AssessmentsLv2)
            .HasForeignKey(x => x.GroupLv2Id)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.GroupLv3)
            .WithMany(x => x.AssessmentsLv3)
            .HasForeignKey(x => x.GroupLv3Id)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
