using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retrack.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLegacyTransactionDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE public.platform_transactions
                    ADD COLUMN IF NOT EXISTS fee_percentage numeric(5,2),
                    ADD COLUMN IF NOT EXISTS payer_id uuid,
                    ADD COLUMN IF NOT EXISTS transaction_amount numeric(18,2);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fee_percentage",
                table: "platform_transactions");

            migrationBuilder.DropColumn(
                name: "payer_id",
                table: "platform_transactions");

            migrationBuilder.DropColumn(
                name: "transaction_amount",
                table: "platform_transactions");
        }
    }
}
