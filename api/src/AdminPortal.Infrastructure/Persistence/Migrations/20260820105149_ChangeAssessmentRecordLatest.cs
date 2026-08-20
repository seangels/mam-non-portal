using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChangeAssessmentRecordLatest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_assessment_record_latests_assessment_sheet_latest_id_assess",
                table: "assessment_record_latests");

            migrationBuilder.DropColumn(
                name: "assessment_code",
                table: "assessment_record_latests");

            migrationBuilder.AddColumn<Guid>(
                name: "assessment_id",
                table: "assessment_record_latests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_assessment_record_latests_assessment_id",
                table: "assessment_record_latests",
                column: "assessment_id");

            migrationBuilder.CreateIndex(
                name: "ix_assessment_record_latests_assessment_sheet_latest_id_assess",
                table: "assessment_record_latests",
                columns: new[] { "assessment_sheet_latest_id", "assessment_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_assessment_record_latests_assessments_assessment_id",
                table: "assessment_record_latests",
                column: "assessment_id",
                principalTable: "assessments",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_assessment_record_latests_assessments_assessment_id",
                table: "assessment_record_latests");

            migrationBuilder.DropIndex(
                name: "ix_assessment_record_latests_assessment_id",
                table: "assessment_record_latests");

            migrationBuilder.DropIndex(
                name: "ix_assessment_record_latests_assessment_sheet_latest_id_assess",
                table: "assessment_record_latests");

            migrationBuilder.DropColumn(
                name: "assessment_id",
                table: "assessment_record_latests");

            migrationBuilder.AddColumn<string>(
                name: "assessment_code",
                table: "assessment_record_latests",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_assessment_record_latests_assessment_sheet_latest_id_assess",
                table: "assessment_record_latests",
                columns: new[] { "assessment_sheet_latest_id", "assessment_code" },
                unique: true);
        }
    }
}
