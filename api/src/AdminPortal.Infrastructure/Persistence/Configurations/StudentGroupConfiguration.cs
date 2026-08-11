using AdminPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdminPortal.Infrastructure.Persistence.Configurations;

internal sealed class StudentGroupConfiguration : IEntityTypeConfiguration<StudentGroup>
{
    public void Configure(EntityTypeBuilder<StudentGroup> builder)
    {
        builder.ToTable("student_groups", table =>
            table.HasCheckConstraint("ck_student_groups_snapshot_version", "snapshot_version >= 1"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.SnapshotVersion).HasDefaultValue(1);
        builder.HasQueryFilter(x => x.DeletedAt == null);
        builder.HasIndex(x => x.Code).IsUnique().HasFilter("deleted_at IS NULL");
        builder.HasIndex(x => new { x.Status, x.CreatedAt, x.Id });
        builder.HasOne(x => x.ResponsibleTeacher)
            .WithMany(x => x.ResponsibleGroups)
            .HasForeignKey(x => x.ResponsibleTeacherId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
