using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retrack.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class DepotPartnershipBlocking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "blocked_by_depot",
                table: "factory_depot_partnerships",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "blocked_by_factory",
                table: "factory_depot_partnerships",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "blocked_by_depot",
                table: "factory_depot_partnerships");

            migrationBuilder.DropColumn(
                name: "blocked_by_factory",
                table: "factory_depot_partnerships");
        }
    }
}
