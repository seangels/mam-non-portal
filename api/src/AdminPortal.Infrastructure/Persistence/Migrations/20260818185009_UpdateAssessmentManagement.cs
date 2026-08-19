using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAssessmentManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_assessments_assessment_groups_group_lv1id",
                table: "assessments");

            migrationBuilder.DropForeignKey(
                name: "fk_assessments_assessment_groups_group_lv2id",
                table: "assessments");

            migrationBuilder.DropForeignKey(
                name: "fk_assessments_assessment_groups_group_lv3id",
                table: "assessments");

            migrationBuilder.DropIndex(
                name: "ix_assessments_group_lv1id",
                table: "assessments");

            migrationBuilder.DropIndex(
                name: "ix_assessments_group_lv2id",
                table: "assessments");

            migrationBuilder.DropColumn(
                name: "group_lv1id",
                table: "assessments");

            migrationBuilder.DropColumn(
                name: "group_lv2id",
                table: "assessments");

            migrationBuilder.RenameColumn(
                name: "group_lv3id",
                table: "assessments",
                newName: "group_id");

            migrationBuilder.RenameIndex(
                name: "ix_assessments_group_lv3id",
                table: "assessments",
                newName: "ix_assessments_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_assessment_groups_parent_id",
                table: "assessment_groups",
                column: "parent_id");

            migrationBuilder.AddForeignKey(
                name: "fk_assessment_groups_assessment_groups_parent_id",
                table: "assessment_groups",
                column: "parent_id",
                principalTable: "assessment_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_assessments_assessment_groups_group_id",
                table: "assessments",
                column: "group_id",
                principalTable: "assessment_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_assessment_groups_assessment_groups_parent_id",
                table: "assessment_groups");

            migrationBuilder.DropForeignKey(
                name: "fk_assessments_assessment_groups_group_id",
                table: "assessments");

            migrationBuilder.DropIndex(
                name: "ix_assessment_groups_parent_id",
                table: "assessment_groups");

            migrationBuilder.RenameColumn(
                name: "group_id",
                table: "assessments",
                newName: "group_lv3id");

            migrationBuilder.RenameIndex(
                name: "ix_assessments_group_id",
                table: "assessments",
                newName: "ix_assessments_group_lv3id");

            migrationBuilder.AddColumn<Guid>(
                name: "group_lv1id",
                table: "assessments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "group_lv2id",
                table: "assessments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_assessments_group_lv1id",
                table: "assessments",
                column: "group_lv1id");

            migrationBuilder.CreateIndex(
                name: "ix_assessments_group_lv2id",
                table: "assessments",
                column: "group_lv2id");

            migrationBuilder.AddForeignKey(
                name: "fk_assessments_assessment_groups_group_lv1id",
                table: "assessments",
                column: "group_lv1id",
                principalTable: "assessment_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_assessments_assessment_groups_group_lv2id",
                table: "assessments",
                column: "group_lv2id",
                principalTable: "assessment_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_assessments_assessment_groups_group_lv3id",
                table: "assessments",
                column: "group_lv3id",
                principalTable: "assessment_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
