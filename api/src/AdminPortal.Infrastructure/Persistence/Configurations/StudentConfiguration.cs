using AdminPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdminPortal.Infrastructure.Persistence.Configurations;

internal sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("students");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StudentCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NickName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Gender).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.GuardianName).HasMaxLength(200);
        builder.Property(x => x.GuardianPhone).HasMaxLength(30);
        builder.HasQueryFilter(x => x.DeletedAt == null);
        builder.HasIndex(x => x.StudentCode)
            .IsUnique()
            .HasFilter("deleted_at IS NULL");
        builder.HasIndex(x => new { x.Status, x.CreatedAt, x.Id });
        builder.HasIndex(x => new { x.GroupId, x.Status, x.Id })
            .HasFilter("deleted_at IS NULL");
        builder.Property(x => x.GroupAssignedByUserId).HasColumnName("group_assigned_by");
        builder.HasOne(x => x.Group).WithMany(x => x.Students)
            .HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.GroupAssignedByUser).WithMany()
            .HasForeignKey(x => x.GroupAssignedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
