using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dashy.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NarrowSavedSearches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Phase 4 (issue #11) narrows a saved search to just a named full-text
            // query string. Drop the source link, tags, time range and refresh interval.
            migrationBuilder.DropForeignKey(
                name: "FK_saved_searches_sources_source_id",
                table: "saved_searches");

            migrationBuilder.DropIndex(
                name: "IX_saved_searches_source_id",
                table: "saved_searches");

            migrationBuilder.DropColumn(
                name: "is_broken",
                table: "saved_searches");

            migrationBuilder.DropColumn(
                name: "refresh_interval_seconds",
                table: "saved_searches");

            migrationBuilder.DropColumn(
                name: "source_id",
                table: "saved_searches");

            migrationBuilder.DropColumn(
                name: "tag_ids",
                table: "saved_searches");

            migrationBuilder.DropColumn(
                name: "time_range",
                table: "saved_searches");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_broken",
                table: "saved_searches",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "refresh_interval_seconds",
                table: "saved_searches",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_id",
                table: "saved_searches",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "tag_ids",
                table: "saved_searches",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "time_range",
                table: "saved_searches",
                type: "TEXT",
                nullable: false,
                defaultValue: "{\"type\":\"relative\",\"value\":\"1h\"}");

            migrationBuilder.CreateIndex(
                name: "IX_saved_searches_source_id",
                table: "saved_searches",
                column: "source_id");

            migrationBuilder.AddForeignKey(
                name: "FK_saved_searches_sources_source_id",
                table: "saved_searches",
                column: "source_id",
                principalTable: "sources",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
