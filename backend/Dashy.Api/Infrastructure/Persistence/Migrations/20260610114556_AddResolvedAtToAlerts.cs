using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dashy.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResolvedAtToAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "resolved_at",
                table: "alerts",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "resolved_at",
                table: "alerts");
        }
    }
}
