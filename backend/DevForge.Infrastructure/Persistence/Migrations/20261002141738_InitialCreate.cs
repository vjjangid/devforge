using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DevForge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    repository_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    branch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    runtime = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_applications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "builds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    artifact_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_builds", x => x.id);
                    table.CheckConstraint("ck_builds_status", "status IN ('Running', 'Succeeded', 'Failed')");
                    table.ForeignKey(
                        name: "fk_builds_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "deployments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    commit_sha = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    current_stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    simulate_failure = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    worker_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    build_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deployments", x => x.id);
                    table.CheckConstraint("ck_deployments_current_stage", "current_stage IN ('Preparing', 'Building', 'Testing', 'Deploying')");
                    table.CheckConstraint("ck_deployments_status", "status IN ('Queued', 'Running', 'Succeeded', 'Failed', 'Cancelled')");
                    table.ForeignKey(
                        name: "fk_deployments_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_deployments_builds_build_id",
                        column: x => x.build_id,
                        principalTable: "builds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "deployment_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    deployment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deployment_logs", x => x.id);
                    table.CheckConstraint("ck_deployment_logs_level", "level IN ('Info', 'Warning', 'Error')");
                    table.CheckConstraint("ck_deployment_logs_stage", "stage IN ('Preparing', 'Building', 'Testing', 'Deploying')");
                    table.ForeignKey(
                        name: "fk_deployment_logs_deployments_deployment_id",
                        column: x => x.deployment_id,
                        principalTable: "deployments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_applications_created_at",
                table: "applications",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ux_applications_name",
                table: "applications",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_builds_application_id_started_at",
                table: "builds",
                columns: new[] { "application_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_deployment_logs_deployment_id_id",
                table: "deployment_logs",
                columns: new[] { "deployment_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_deployments_application_id_number",
                table: "deployments",
                columns: new[] { "application_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_deployments_build_id",
                table: "deployments",
                column: "build_id");

            migrationBuilder.CreateIndex(
                name: "ix_deployments_created_at",
                table: "deployments",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_deployments_status_created_at",
                table: "deployments",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_deployments_one_active_per_application",
                table: "deployments",
                column: "application_id",
                unique: true,
                filter: "status IN ('Queued', 'Running')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deployment_logs");

            migrationBuilder.DropTable(
                name: "deployments");

            migrationBuilder.DropTable(
                name: "builds");

            migrationBuilder.DropTable(
                name: "applications");
        }
    }
}
