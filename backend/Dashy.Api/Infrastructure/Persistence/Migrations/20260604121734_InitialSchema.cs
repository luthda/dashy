using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dashy.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "TEXT", nullable: false),
                    config = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sources", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    color = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "#6366f1"),
                    filters = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "{}"),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "alerts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    source_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    query = table.Column<string>(type: "TEXT", nullable: false),
                    check_interval_seconds = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 300),
                    threshold = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    enabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    last_checked_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    status = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Ok"),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alerts", x => x.id);
                    table.ForeignKey(
                        name: "FK_alerts_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "saved_searches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    source_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    query = table.Column<string>(type: "TEXT", nullable: false),
                    tag_ids = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
                    time_range = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "{\"type\":\"relative\",\"value\":\"1h\"}"),
                    refresh_interval_seconds = table.Column<int>(type: "INTEGER", nullable: true),
                    is_broken = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "datetime('now')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_searches", x => x.id);
                    table.ForeignKey(
                        name: "FK_saved_searches_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "alert_firings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    alert_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    fired_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    result_count = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_firings", x => x.id);
                    table.ForeignKey(
                        name: "FK_alert_firings_alerts_alert_id",
                        column: x => x.alert_id,
                        principalTable: "alerts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_alert_firings_alert_id",
                table: "alert_firings",
                column: "alert_id");

            migrationBuilder.CreateIndex(
                name: "IX_alerts_source_id",
                table: "alerts",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "IX_saved_searches_source_id",
                table: "saved_searches",
                column: "source_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alert_firings");

            migrationBuilder.DropTable(
                name: "saved_searches");

            migrationBuilder.DropTable(
                name: "tags");

            migrationBuilder.DropTable(
                name: "alerts");

            migrationBuilder.DropTable(
                name: "sources");
        }
    }
}
