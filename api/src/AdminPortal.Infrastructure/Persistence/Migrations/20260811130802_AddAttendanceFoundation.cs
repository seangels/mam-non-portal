using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace AdminPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "group_assigned_at",
                table: "students",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "group_assigned_by",
                table: "students",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "group_id",
                table: "students",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "teachers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attendance_edit_window_days = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)7),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_teachers", x => x.id);
                    table.CheckConstraint("ck_teachers_attendance_edit_window_days", "attendance_edit_window_days BETWEEN 1 AND 7");
                    table.ForeignKey(
                        name: "fk_teachers_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO teachers (id, user_id, attendance_edit_window_days, created_at, updated_at)
                SELECT gen_random_uuid(), id, 7, created_at, updated_at
                FROM users
                WHERE role = 'Teacher';
                """);

            migrationBuilder.CreateTable(
                name: "student_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    responsible_teacher_id = table.Column<Guid>(type: "uuid", nullable: true),
                    snapshot_version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    snapshot_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_student_groups", x => x.id);
                    table.CheckConstraint("ck_student_groups_snapshot_version", "snapshot_version >= 1");
                    table.ForeignKey(
                        name: "fk_student_groups_teachers_responsible_teacher_id",
                        column: x => x.responsible_teacher_id,
                        principalTable: "teachers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attendance_sheets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attendance_date = table.Column<DateOnly>(type: "date", nullable: false),
                    group_code_snapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    group_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    responsible_teacher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    responsible_teacher_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    snapshot_source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    source_snapshot_version = table.Column<int>(type: "integer", nullable: true),
                    recovery_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendance_sheets", x => x.id);
                    table.UniqueConstraint("ak_attendance_sheets_id_attendance_date", x => new { x.id, x.attendance_date });
                    table.CheckConstraint("ck_attendance_sheets_source", "(snapshot_source = 'CurrentSnapshot' AND source_snapshot_version IS NOT NULL AND recovery_reason IS NULL) OR (snapshot_source = 'HistoricalRecovery' AND source_snapshot_version IS NULL AND recovery_reason IS NOT NULL AND recovery_reason = btrim(recovery_reason) AND recovery_reason <> '')");
                    table.CheckConstraint("ck_attendance_sheets_version", "version >= 1");
                    table.ForeignKey(
                        name: "fk_attendance_sheets_student_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "student_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_sheets_teachers_responsible_teacher_id",
                        column: x => x.responsible_teacher_id,
                        principalTable: "teachers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_sheets_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_sheets_users_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attendance_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sheet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attendance_date = table.Column<DateOnly>(type: "date", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_code_snapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    full_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nick_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    half_day_part = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_excused = table.Column<bool>(type: "boolean", nullable: true),
                    duration_minutes = table.Column<int>(type: "integer", nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendance_records", x => x.id);
                    table.CheckConstraint("ck_attendance_records_status_fields", "(status = 'Present' AND half_day_part IS NULL AND is_excused IS NULL AND duration_minutes IS NULL) OR (status = 'AbsentFullDay' AND half_day_part IS NULL AND is_excused IS NOT NULL AND duration_minutes IS NULL) OR (status = 'AbsentHalfDay' AND half_day_part IS NOT NULL AND is_excused IS NOT NULL AND duration_minutes IS NULL) OR (status = 'OneToOneHour' AND half_day_part IS NULL AND is_excused IS NULL AND duration_minutes = 60)");
                    table.ForeignKey(
                        name: "fk_attendance_records_attendance_sheets_sheet_id_attendance_da",
                        columns: x => new { x.sheet_id, x.attendance_date },
                        principalTable: "attendance_sheets",
                        principalColumns: new[] { "id", "attendance_date" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_records_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attendance_records_users_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_students_group_assigned_by_user_id",
                table: "students",
                column: "group_assigned_by");

            migrationBuilder.CreateIndex(
                name: "ix_students_group_id_status_id",
                table: "students",
                columns: new[] { "group_id", "status", "id" },
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_records_sheet_id_attendance_date",
                table: "attendance_records",
                columns: new[] { "sheet_id", "attendance_date" });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_records_sheet_id_student_id",
                table: "attendance_records",
                columns: new[] { "sheet_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_attendance_records_student_id_attendance_date",
                table: "attendance_records",
                columns: new[] { "student_id", "attendance_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_attendance_records_updated_by_user_id",
                table: "attendance_records",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_sheets_attendance_date_group_id",
                table: "attendance_sheets",
                columns: new[] { "attendance_date", "group_id" });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_sheets_created_by_user_id",
                table: "attendance_sheets",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_sheets_group_id_attendance_date",
                table: "attendance_sheets",
                columns: new[] { "group_id", "attendance_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_attendance_sheets_responsible_teacher_id",
                table: "attendance_sheets",
                column: "responsible_teacher_id");

            migrationBuilder.CreateIndex(
                name: "ix_attendance_sheets_updated_by_user_id",
                table: "attendance_sheets",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_groups_code",
                table: "student_groups",
                column: "code",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_student_groups_responsible_teacher_id",
                table: "student_groups",
                column: "responsible_teacher_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_groups_status_created_at_id",
                table: "student_groups",
                columns: new[] { "status", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_teachers_user_id",
                table: "teachers",
                column: "user_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_students_student_groups_group_id",
                table: "students",
                column: "group_id",
                principalTable: "student_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_students_users_group_assigned_by_user_id",
                table: "students",
                column: "group_assigned_by",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_students_student_groups_group_id",
                table: "students");

            migrationBuilder.DropForeignKey(
                name: "fk_students_users_group_assigned_by_user_id",
                table: "students");

            migrationBuilder.DropTable(
                name: "attendance_records");

            migrationBuilder.DropTable(
                name: "attendance_sheets");

            migrationBuilder.DropTable(
                name: "student_groups");

            migrationBuilder.DropTable(
                name: "teachers");

            migrationBuilder.DropIndex(
                name: "ix_students_group_assigned_by_user_id",
                table: "students");

            migrationBuilder.DropIndex(
                name: "ix_students_group_id_status_id",
                table: "students");

            migrationBuilder.DropColumn(
                name: "group_assigned_at",
                table: "students");

            migrationBuilder.DropColumn(
                name: "group_assigned_by",
                table: "students");

            migrationBuilder.DropColumn(
                name: "group_id",
                table: "students");
        }
    }
}
