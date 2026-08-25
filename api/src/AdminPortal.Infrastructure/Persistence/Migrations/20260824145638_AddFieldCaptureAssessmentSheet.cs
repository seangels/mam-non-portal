using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldCaptureAssessmentSheet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "assessment_sheet_plan",
                table: "assessment_sheets",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "assessment_sheet_plan",
                table: "assessment_sheets");
        }
    }
}
