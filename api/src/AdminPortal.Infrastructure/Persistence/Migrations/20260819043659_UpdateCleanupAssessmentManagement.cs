using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCleanupAssessmentManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_assessments_assessment_groups_group_id",
                table: "assessments");

            migrationBuilder.DropTable(
                name: "assessment_groups");

            migrationBuilder.DropIndex(
                name: "ix_assessments_group_id",
                table: "assessments");

            migrationBuilder.DropColumn(
                name: "group_id",
                table: "assessments");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "assessments",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<string>(
                name: "group_lv1name",
                table: "assessments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "group_lv2name",
                table: "assessments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "group_lv3name",
                table: "assessments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "group_lv1name",
                table: "assessments");

            migrationBuilder.DropColumn(
                name: "group_lv2name",
                table: "assessments");

            migrationBuilder.DropColumn(
                name: "group_lv3name",
                table: "assessments");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "assessments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AddColumn<Guid>(
                name: "group_id",
                table: "assessments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "assessment_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: true),
                    level = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assessment_groups", x => x.id);
                    table.ForeignKey(
                        name: "fk_assessment_groups_assessment_groups_parent_id",
                        column: x => x.parent_id,
                        principalTable: "assessment_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_assessment_groups_users_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_assessments_group_id",
                table: "assessments",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_assessment_groups_parent_id",
                table: "assessment_groups",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_assessment_groups_updated_by_user_id",
                table: "assessment_groups",
                column: "updated_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_assessments_assessment_groups_group_id",
                table: "assessments",
                column: "group_id",
                principalTable: "assessment_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
