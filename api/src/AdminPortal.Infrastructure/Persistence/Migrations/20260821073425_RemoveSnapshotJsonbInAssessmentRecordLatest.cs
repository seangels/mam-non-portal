using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSnapshotJsonbInAssessmentRecordLatest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "assessment_row_index",
                table: "assessment_record_latests");

            migrationBuilder.DropColumn(
                name: "assessment_snapshot",
                table: "assessment_record_latests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "assessment_row_index",
                table: "assessment_record_latests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "assessment_snapshot",
                table: "assessment_record_latests",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");
        }
    }
}
