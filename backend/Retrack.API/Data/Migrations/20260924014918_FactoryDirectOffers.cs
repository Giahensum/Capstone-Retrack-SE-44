using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retrack.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class FactoryDirectOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "direct_offer_factory_id",
                table: "inventory_batches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_inventory_batches_factories_direct_offer_factory_id",
                table: "inventory_batches",
                column: "direct_offer_factory_id",
                principalTable: "factories",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_inventory_batches_factories_direct_offer_factory_id",
                table: "inventory_batches");

            migrationBuilder.DropColumn(
                name: "direct_offer_factory_id",
                table: "inventory_batches");
        }
    }
}
