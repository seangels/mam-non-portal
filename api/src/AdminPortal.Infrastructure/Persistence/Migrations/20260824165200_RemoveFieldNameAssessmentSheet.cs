using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFieldNameAssessmentSheet : Migration
    {
        private static readonly string[] AssessmentSheetUniqueIndexColumnsDown = new[] { "student_id", "name" };
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_assessment_sheets_student_id_name",
                table: "assessment_sheets");

            migrationBuilder.DropColumn(
                name: "name",
                table: "assessment_sheets");

            migrationBuilder.CreateIndex(
                name: "ix_assessment_sheets_student_id",
                table: "assessment_sheets",
                column: "student_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_assessment_sheets_student_id",
                table: "assessment_sheets");

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "assessment_sheets",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_assessment_sheets_student_id_name",
                table: "assessment_sheets",
                columns: AssessmentSheetUniqueIndexColumnsDown,
                unique: true);
        }
    }
}
