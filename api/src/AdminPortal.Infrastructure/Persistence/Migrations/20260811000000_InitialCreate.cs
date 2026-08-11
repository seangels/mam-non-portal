using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminPortal.Infrastructure.Persistence.Migrations;

public partial class InitialCreate : Migration
{
    private static readonly string[] StatusCreatedAtIdColumns = ["status", "created_at", "id"];
    private static readonly string[] SessionRetentionColumns = ["user_id", "revoked_at", "refresh_token_expires_at"];
    private static readonly string[] AuditEntityColumns = ["entity_type", "entity_id", "created_at"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "students",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                student_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                nick_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                date_of_birth = table.Column<DateOnly>(type: "date", nullable: false),
                gender = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                guardian_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                guardian_phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                note = table.Column<string>(type: "text", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_students", x => x.id));

        migrationBuilder.CreateTable(
            name: "users",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                normalized_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                password_hash = table.Column<string>(type: "text", nullable: false),
                full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                phone_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                failed_login_count = table.Column<int>(type: "integer", nullable: false),
                lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_users", x => x.id));

        migrationBuilder.CreateTable(
            name: "audit_logs",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                old_values = table.Column<string>(type: "jsonb", nullable: true),
                new_values = table.Column<string>(type: "jsonb", nullable: true),
                ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_audit_logs", x => x.id);
                table.ForeignKey("fk_audit_logs_users_actor_user_id", x => x.actor_user_id, "users", "id", onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "auth_sessions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                refresh_token_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                refresh_token_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                last_refreshed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                created_by_ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_auth_sessions", x => x.id);
                table.ForeignKey("fk_auth_sessions_users_user_id", x => x.user_id, "users", "id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("ix_users_normalized_email", "users", "normalized_email", unique: true, filter: "deleted_at IS NULL");
        migrationBuilder.CreateIndex("ix_users_status_created_at_id", "users", StatusCreatedAtIdColumns);
        migrationBuilder.CreateIndex("ix_students_student_code", "students", "student_code", unique: true, filter: "deleted_at IS NULL");
        migrationBuilder.CreateIndex("ix_students_status_created_at_id", "students", StatusCreatedAtIdColumns);
        migrationBuilder.CreateIndex("ix_auth_sessions_refresh_token_hash", "auth_sessions", "refresh_token_hash", unique: true);
        migrationBuilder.CreateIndex("ix_auth_sessions_user_id_revoked_at_refresh_token_expires_at", "auth_sessions", SessionRetentionColumns);
        migrationBuilder.CreateIndex("ix_audit_logs_actor_user_id", "audit_logs", "actor_user_id");
        migrationBuilder.CreateIndex("ix_audit_logs_entity_type_entity_id_created_at", "audit_logs", AuditEntityColumns);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("audit_logs");
        migrationBuilder.DropTable("auth_sessions");
        migrationBuilder.DropTable("students");
        migrationBuilder.DropTable("users");
    }
}
