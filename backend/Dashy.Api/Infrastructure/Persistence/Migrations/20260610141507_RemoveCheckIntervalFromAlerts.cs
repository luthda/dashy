using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dashy.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCheckIntervalFromAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "check_interval_seconds",
                table: "alerts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "check_interval_seconds",
                table: "alerts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 300);
        }
    }
}
