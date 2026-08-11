using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "note",
                table: "teachers",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "teacher_code",
                table: "teachers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "teachers",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(
                """
                UPDATE teachers
                SET teacher_code = 'GV-MIG-' || upper(replace(id::text, '-', ''));
                """);

            migrationBuilder.AlterColumn<string>(
                name: "teacher_code",
                table: "teachers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_teachers_teacher_code",
                table: "teachers",
                column: "teacher_code",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_teachers_teacher_code",
                table: "teachers",
                sql: "teacher_code = upper(btrim(teacher_code)) AND teacher_code <> ''");

            migrationBuilder.AddCheckConstraint(
                name: "ck_teachers_version",
                table: "teachers",
                sql: "version >= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_teachers_teacher_code",
                table: "teachers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_teachers_teacher_code",
                table: "teachers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_teachers_version",
                table: "teachers");

            migrationBuilder.DropColumn(
                name: "note",
                table: "teachers");

            migrationBuilder.DropColumn(
                name: "teacher_code",
                table: "teachers");

            migrationBuilder.DropColumn(
                name: "version",
                table: "teachers");
        }
    }
}
