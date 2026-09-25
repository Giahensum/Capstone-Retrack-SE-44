using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retrack.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class DepotBatchCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "depot_batch_number");

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "inventory_batches",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_batches_code",
                table: "inventory_batches",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_inventory_batches_code",
                table: "inventory_batches");

            migrationBuilder.DropColumn(
                name: "code",
                table: "inventory_batches");

            migrationBuilder.DropSequence(
                name: "depot_batch_number");
        }
    }
}
