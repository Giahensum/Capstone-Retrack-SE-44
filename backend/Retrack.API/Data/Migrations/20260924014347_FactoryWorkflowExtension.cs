using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retrack.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class FactoryWorkflowExtension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE inventory_batches ADD COLUMN IF NOT EXISTS actual_weight_kg NUMERIC;
                ALTER TABLE batch_quality_checks ADD COLUMN IF NOT EXISTS actual_weight_kg NUMERIC;
                """);

            migrationBuilder.AddColumn<decimal>(
                name: "agreed_price_per_kg",
                table: "inventory_batches",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "factory_decided_at",
                table: "inventory_batches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "factory_received_at",
                table: "inventory_batches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "gross_amount",
                table: "inventory_batches",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "net_amount",
                table: "inventory_batches",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(name: "payment_reference", table: "inventory_batches", type: "character varying(200)", maxLength: 200, nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "platform_fee_amount",
                table: "inventory_batches",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_reason",
                table: "inventory_batches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "settled_at",
                table: "inventory_batches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "note",
                table: "factory_demands",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "factory_demands",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<string>(
                name: "accepted_materials",
                table: "factories",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "business_license_url",
                table: "factories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "capacity_kg_per_month",
                table: "factories",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "contact_phone",
                table: "factories",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "environmental_license_url",
                table: "factories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "industrial_zone",
                table: "factories",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "minimum_purity_percent",
                table: "factories",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "tax_code",
                table: "factories",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "contamination_percent",
                table: "batch_quality_checks",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "difference_percentage",
                table: "batch_quality_checks",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "gross_weight_kg",
                table: "batch_quality_checks",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invoice_file_url",
                table: "batch_quality_checks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(name: "invoice_number", table: "batch_quality_checks", type: "character varying(100)", maxLength: 100, nullable: true);

            migrationBuilder.AddColumn<string>(name: "invoice_status", table: "batch_quality_checks", type: "character varying(30)", maxLength: 30, nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "moisture_percent",
                table: "batch_quality_checks",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "purity_percent",
                table: "batch_quality_checks",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "quality_note",
                table: "batch_quality_checks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(name: "resolution", table: "batch_quality_checks", type: "character varying(20)", maxLength: 20, nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "tare_weight_kg",
                table: "batch_quality_checks",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ticket_image_url",
                table: "batch_quality_checks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(name: "ticket_number", table: "batch_quality_checks", type: "character varying(100)", maxLength: 100, nullable: true);

            migrationBuilder.CreateTable(
                name: "market_prices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    price_per_kg = table.Column<decimal>(type: "numeric", nullable: false),
                    effective_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    source = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_market_prices", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Keep Factory workflow data during rollback to avoid losing operational records.
        }
    }
}
