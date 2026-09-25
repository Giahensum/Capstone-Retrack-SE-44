using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retrack.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class DepotPaymentUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.CreateIndex(
                name: "IX_platform_transactions_source_type_source_id",
                table: "platform_transactions",
                columns: new[] { "source_type", "source_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_platform_transactions_source_type_source_id",
                table: "platform_transactions");
        }
    }
}

