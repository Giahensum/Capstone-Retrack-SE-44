using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retrack.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class DepotProfileAndFeeInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "contact_phone",
                table: "depots",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "depots",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tax_code",
                table: "depots",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "platform_fee_invoices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    payment_proof_url = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_fee_invoices", x => x.id);
                    table.CheckConstraint("CK_fee_invoice_amount", "amount >= 0");
                    table.CheckConstraint("CK_fee_invoice_status", "status IN ('UNPAID', 'SUBMITTED', 'PAID')");
                    table.ForeignKey(
                        name: "FK_platform_fee_invoices_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("UPDATE system_configs SET config_value = '5.00', description = 'Phí nền tảng mặc định 5%' WHERE config_key = 'PLATFORM_FEE_PERCENTAGE' AND config_value = '1.00';");

            migrationBuilder.CreateIndex(
                name: "IX_platform_fee_invoices_owner_id_period_start",
                table: "platform_fee_invoices",
                columns: new[] { "owner_id", "period_start" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_fee_invoices");

            migrationBuilder.DropColumn(
                name: "contact_phone",
                table: "depots");

            migrationBuilder.DropColumn(
                name: "description",
                table: "depots");

            migrationBuilder.DropColumn(
                name: "tax_code",
                table: "depots");

            // Preserve runtime fee configuration on rollback.
        }
    }
}

