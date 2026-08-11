using AdminPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdminPortal.Infrastructure.Persistence.Configurations;

internal sealed class AttendanceSheetConfiguration : IEntityTypeConfiguration<AttendanceSheet>
{
    public void Configure(EntityTypeBuilder<AttendanceSheet> builder)
    {
        builder.ToTable("attendance_sheets", table =>
        {
            table.HasCheckConstraint("ck_attendance_sheets_version", "version >= 1");
            table.HasCheckConstraint(
                "ck_attendance_sheets_source",
                "(snapshot_source = 'CurrentSnapshot' AND source_snapshot_version IS NOT NULL AND recovery_reason IS NULL) OR " +
                "(snapshot_source = 'HistoricalRecovery' AND source_snapshot_version IS NULL AND " +
                "recovery_reason IS NOT NULL AND recovery_reason = btrim(recovery_reason) AND recovery_reason <> '')");
        });
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.Id, x.AttendanceDate });
        builder.Property(x => x.GroupCodeSnapshot).HasMaxLength(50).IsRequired();
        builder.Property(x => x.GroupNameSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ResponsibleTeacherIdSnapshot).HasColumnName("responsible_teacher_id");
        builder.Property(x => x.ResponsibleTeacherNameSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SnapshotSource).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.HistoricalRecoveryReason).HasColumnName("recovery_reason").HasMaxLength(500);
        builder.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
        builder.HasIndex(x => new { x.GroupId, x.AttendanceDate }).IsUnique();
        builder.HasIndex(x => new { x.AttendanceDate, x.GroupId });
        builder.HasOne(x => x.Group).WithMany(x => x.AttendanceSheets)
            .HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ResponsibleTeacherSnapshot).WithMany()
            .HasForeignKey(x => x.ResponsibleTeacherIdSnapshot).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedByUser).WithMany()
            .HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UpdatedByUser).WithMany()
            .HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
