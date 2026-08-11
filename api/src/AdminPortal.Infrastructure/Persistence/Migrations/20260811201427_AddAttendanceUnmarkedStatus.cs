using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceUnmarkedStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_attendance_records_status_fields",
                table: "attendance_records");

            migrationBuilder.AddCheckConstraint(
                name: "ck_attendance_records_status_fields",
                table: "attendance_records",
                sql: "(status = 'Present' AND half_day_part IS NULL AND is_excused IS NULL AND duration_minutes IS NULL) OR (status = 'AbsentFullDay' AND half_day_part IS NULL AND is_excused IS NOT NULL AND duration_minutes IS NULL) OR (status = 'AbsentHalfDay' AND (half_day_part IS NULL OR half_day_part IN ('Morning', 'Afternoon')) AND is_excused IS NOT NULL AND duration_minutes IS NULL) OR (status = 'OneToOneHour' AND half_day_part IS NULL AND is_excused IS NULL AND duration_minutes = 60) OR (status = 'Unmarked' AND half_day_part IS NULL AND is_excused IS NULL AND duration_minutes IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_attendance_records_status_fields",
                table: "attendance_records");

            migrationBuilder.AddCheckConstraint(
                name: "ck_attendance_records_status_fields",
                table: "attendance_records",
                sql: "(status = 'Present' AND half_day_part IS NULL AND is_excused IS NULL AND duration_minutes IS NULL) OR (status = 'AbsentFullDay' AND half_day_part IS NULL AND is_excused IS NOT NULL AND duration_minutes IS NULL) OR (status = 'AbsentHalfDay' AND half_day_part IS NOT NULL AND is_excused IS NOT NULL AND duration_minutes IS NULL) OR (status = 'OneToOneHour' AND half_day_part IS NULL AND is_excused IS NULL AND duration_minutes = 60)");
        }
    }
}
