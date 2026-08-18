using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssessmentManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "assessment_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    display_order = table.Column<int>(type: "integer", nullable: true),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assessment_groups", x => x.id);
                    table.ForeignKey(
                        name: "fk_assessment_groups_users_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assessments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    group_lv1id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_lv2id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_lv3id = table.Column<Guid>(type: "uuid", nullable: false),
                    row_index = table.Column<int>(type: "integer", nullable: true),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assessments", x => x.id);
                    table.ForeignKey(
                        name: "fk_assessments_assessment_groups_group_lv1id",
                        column: x => x.group_lv1id,
                        principalTable: "assessment_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_assessments_assessment_groups_group_lv2id",
                        column: x => x.group_lv2id,
                        principalTable: "assessment_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_assessments_assessment_groups_group_lv3id",
                        column: x => x.group_lv3id,
                        principalTable: "assessment_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_assessments_users_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_assessment_groups_updated_by_user_id",
                table: "assessment_groups",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_assessments_group_lv1id",
                table: "assessments",
                column: "group_lv1id");

            migrationBuilder.CreateIndex(
                name: "ix_assessments_group_lv2id",
                table: "assessments",
                column: "group_lv2id");

            migrationBuilder.CreateIndex(
                name: "ix_assessments_group_lv3id",
                table: "assessments",
                column: "group_lv3id");

            migrationBuilder.CreateIndex(
                name: "ix_assessments_updated_by_user_id",
                table: "assessments",
                column: "updated_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assessments");

            migrationBuilder.DropTable(
                name: "assessment_groups");
        }
    }
}
