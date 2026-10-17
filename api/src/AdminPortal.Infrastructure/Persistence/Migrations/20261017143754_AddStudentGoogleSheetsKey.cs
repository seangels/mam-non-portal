using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentGoogleSheetsKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "google_sheets_student_id",
                table: "students",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "google_sheets_student_id",
                table: "students");
        }
    }
}
