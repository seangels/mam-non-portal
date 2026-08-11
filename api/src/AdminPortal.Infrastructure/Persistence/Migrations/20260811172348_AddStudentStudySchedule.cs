using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentStudySchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "study_mode",
                table: "students",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "study_weekday_mask",
                table: "students",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "students",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(
                """
                UPDATE students
                SET study_mode = 'FullDay',
                    study_weekday_mask = 63,
                    version = 1;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "study_mode",
                table: "students",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<short>(
                name: "study_weekday_mask",
                table: "students",
                type: "smallint",
                nullable: false,
                oldClrType: typeof(short),
                oldType: "smallint",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_students_study_mode",
                table: "students",
                sql: "study_mode IN ('OneToOne', 'FullDay')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_students_study_weekday_mask",
                table: "students",
                sql: "study_weekday_mask BETWEEN 1 AND 63");

            migrationBuilder.AddCheckConstraint(
                name: "ck_students_version",
                table: "students",
                sql: "version >= 1");

            migrationBuilder.Sql(
                """
                UPDATE student_groups AS student_group
                SET snapshot_version = student_group.snapshot_version + 1,
                    snapshot_changed_at = CURRENT_TIMESTAMP,
                    updated_at = CURRENT_TIMESTAMP
                WHERE student_group.deleted_at IS NULL
                  AND EXISTS (
                      SELECT 1
                      FROM students AS student
                      WHERE student.group_id = student_group.id
                        AND student.status = 'Active'
                        AND student.deleted_at IS NULL
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_students_study_mode",
                table: "students");

            migrationBuilder.DropCheckConstraint(
                name: "ck_students_study_weekday_mask",
                table: "students");

            migrationBuilder.DropCheckConstraint(
                name: "ck_students_version",
                table: "students");

            migrationBuilder.DropColumn(
                name: "study_mode",
                table: "students");

            migrationBuilder.DropColumn(
                name: "study_weekday_mask",
                table: "students");

            migrationBuilder.DropColumn(
                name: "version",
                table: "students");
        }
    }
}
