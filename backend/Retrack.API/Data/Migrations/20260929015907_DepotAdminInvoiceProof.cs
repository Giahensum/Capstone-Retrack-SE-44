using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retrack.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class DepotAdminInvoiceProof : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "payment_proof_url",
                table: "platform_invoices",
                type: "text",
                nullable: true);

            // Chạy phần chuyển dữ liệu sau khi cả hai cột đã tồn tại.

            migrationBuilder.AddColumn<DateTime>(
                name: "submitted_at",
                table: "platform_invoices",
                type: "timestamp with time zone",
                nullable: true);
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (
                        SELECT 1 FROM platform_fee_invoices old
                        JOIN platform_invoices current ON current.payer_id = old.owner_id
                            AND current.period_year = EXTRACT(YEAR FROM old.period_start)::int
                            AND current.period_month = EXTRACT(MONTH FROM old.period_start)::int
                        WHERE current.total_fee_amount <> old.amount
                    ) THEN
                        RAISE EXCEPTION 'Hoa don Depot/Admin cung ky khac so tien: can doi soat truoc khi chuyen du lieu';
                    END IF;
                END $$;
                INSERT INTO platform_invoices
                    (id, payer_id, period_year, period_month, total_fee_amount, status, created_at, payment_proof_url, submitted_at)
                SELECT id, owner_id, EXTRACT(YEAR FROM period_start)::int, EXTRACT(MONTH FROM period_start)::int,
                    amount, CASE WHEN status = 'UNPAID' THEN 'PENDING' ELSE status END, created_at, payment_proof_url, submitted_at
                FROM platform_fee_invoices
                ON CONFLICT (payer_id, period_year, period_month) DO UPDATE SET
                    payment_proof_url = COALESCE(platform_invoices.payment_proof_url, EXCLUDED.payment_proof_url),
                    submitted_at = COALESCE(platform_invoices.submitted_at, EXCLUDED.submitted_at),
                    status = CASE WHEN platform_invoices.status = 'PAID' OR EXCLUDED.status = 'PAID' THEN 'PAID'
                        WHEN platform_invoices.status = 'SUBMITTED' OR EXCLUDED.status = 'SUBMITTED' THEN 'SUBMITTED'
                        ELSE platform_invoices.status END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "payment_proof_url",
                table: "platform_invoices");

            migrationBuilder.DropColumn(
                name: "submitted_at",
                table: "platform_invoices");
        }
    }
}
