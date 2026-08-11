using AdminPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdminPortal.Infrastructure.Persistence.Configurations;

internal sealed class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("attendance_records", table => table.HasCheckConstraint(
            "ck_attendance_records_status_fields",
            "(status = 'Present' AND half_day_part IS NULL AND is_excused IS NULL AND duration_minutes IS NULL) OR " +
            "(status = 'AbsentFullDay' AND half_day_part IS NULL AND is_excused IS NOT NULL AND duration_minutes IS NULL) OR " +
            "(status = 'AbsentHalfDay' AND half_day_part IS NOT NULL AND is_excused IS NOT NULL AND duration_minutes IS NULL) OR " +
            "(status = 'OneToOneHour' AND half_day_part IS NULL AND is_excused IS NULL AND duration_minutes = 60)"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StudentCodeSnapshot).HasMaxLength(50).IsRequired();
        builder.Property(x => x.StudentFullNameSnapshot).HasColumnName("full_name_snapshot").HasMaxLength(200).IsRequired();
        builder.Property(x => x.StudentNickNameSnapshot).HasColumnName("nick_name_snapshot").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.HalfDayPart).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasIndex(x => new { x.SheetId, x.StudentId }).IsUnique();
        builder.HasIndex(x => new { x.StudentId, x.AttendanceDate }).IsUnique();
        builder.HasOne(x => x.Sheet).WithMany(x => x.Records)
            .HasForeignKey(x => new { x.SheetId, x.AttendanceDate })
            .HasPrincipalKey(x => new { x.Id, x.AttendanceDate })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Student).WithMany(x => x.AttendanceRecords)
            .HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UpdatedByUser).WithMany()
            .HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
