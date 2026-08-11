using AdminPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdminPortal.Infrastructure.Persistence.Configurations;

internal sealed class TeacherConfiguration : IEntityTypeConfiguration<Teacher>
{
    public void Configure(EntityTypeBuilder<Teacher> builder)
    {
        builder.ToTable("teachers", table =>
            table.HasCheckConstraint("ck_teachers_attendance_edit_window_days", "attendance_edit_window_days BETWEEN 1 AND 7"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AttendanceEditWindowDays).HasDefaultValue((short)7);
        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasOne(x => x.User)
            .WithOne(x => x.TeacherProfile)
            .HasForeignKey<Teacher>(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
