using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssessmentSheetManagement : Migration
    {
        private static readonly string[] AssessmentRecordLatestUniqueIndexColumns = { "assessment_sheet_latest_id", "assessment_code" };
        private static readonly string[] AssessmentSheetUniqueIndexColumns = { "student_id", "name" };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "assessment_sheet_latests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    assessment_sheet_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    responsible_teacher_id = table.Column<Guid>(type: "uuid", nullable: true),
                    responsible_teacher_full_name_snapshot = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    start_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    due_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    done_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    feedback = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    student_snapshot = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assessment_sheet_latests", x => x.id);
                    table.ForeignKey(
                        name: "fk_assessment_sheet_latests_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assessment_sheet_latests_teachers_responsible_teacher_id",
                        column: x => x.responsible_teacher_id,
                        principalTable: "teachers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assessment_sheet_latests_users_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "assessment_sheets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    assessment_sheet_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    responsible_teacher_id = table.Column<Guid>(type: "uuid", nullable: true),
                    responsible_teacher_full_name_snapshot = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    start_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    due_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    done_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    submission_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    feedback = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    plan_file_link_pdf = table.Column<string>(type: "text", nullable: true),
                    result_file_link_pdf = table.Column<string>(type: "text", nullable: true),
                    assessment_sheet_spreadsheet_id = table.Column<string>(type: "text", nullable: true),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    student_snapshot = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assessment_sheets", x => x.id);
                    table.ForeignKey(
                        name: "fk_assessment_sheets_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assessment_sheets_teachers_responsible_teacher_id",
                        column: x => x.responsible_teacher_id,
                        principalTable: "teachers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assessment_sheets_users_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "assessment_record_latests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assessment_sheet_latest_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assessment_row_index = table.Column<int>(type: "integer", nullable: true),
                    assessment_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    latest_grade = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    assessment_snapshot = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assessment_record_latests", x => x.id);
                    table.ForeignKey(
                        name: "fk_assessment_record_latests_assessment_sheet_latests_assessme",
                        column: x => x.assessment_sheet_latest_id,
                        principalTable: "assessment_sheet_latests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "assessment_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assessment_sheet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assessment_row_index = table.Column<int>(type: "integer", nullable: true),
                    plan_grade = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    final_grade = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    plan_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    final_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    assessment_snapshot = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assessment_records", x => x.id);
                    table.ForeignKey(
                        name: "fk_assessment_records_assessment_sheets_assessment_sheet_id",
                        column: x => x.assessment_sheet_id,
                        principalTable: "assessment_sheets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assessment_records_users_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_assessment_record_latests_assessment_sheet_latest_id_assess",
                table: "assessment_record_latests",
                columns: AssessmentRecordLatestUniqueIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_assessment_records_assessment_sheet_id",
                table: "assessment_records",
                column: "assessment_sheet_id");

            migrationBuilder.CreateIndex(
                name: "ix_assessment_records_updated_by_user_id",
                table: "assessment_records",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_assessment_sheet_latests_responsible_teacher_id",
                table: "assessment_sheet_latests",
                column: "responsible_teacher_id");

            migrationBuilder.CreateIndex(
                name: "ix_assessment_sheet_latests_student_id",
                table: "assessment_sheet_latests",
                column: "student_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_assessment_sheet_latests_updated_by_user_id",
                table: "assessment_sheet_latests",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_assessment_sheets_responsible_teacher_id",
                table: "assessment_sheets",
                column: "responsible_teacher_id");

            migrationBuilder.CreateIndex(
                name: "ix_assessment_sheets_student_id_name",
                table: "assessment_sheets",
                columns: AssessmentSheetUniqueIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_assessment_sheets_updated_by_user_id",
                table: "assessment_sheets",
                column: "updated_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assessment_record_latests");

            migrationBuilder.DropTable(
                name: "assessment_records");

            migrationBuilder.DropTable(
                name: "assessment_sheet_latests");

            migrationBuilder.DropTable(
                name: "assessment_sheets");
        }
    }
}
