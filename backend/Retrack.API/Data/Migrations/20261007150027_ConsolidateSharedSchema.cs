using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Retrack.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateSharedSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Đối soát và chuyển nốt dữ liệu có thể phát sinh sau migration hợp nhất trước đó.
            // Nếu cùng kỳ nhưng lệch số tiền, dừng migration để người vận hành kiểm tra.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (
                        SELECT 1 FROM platform_fee_invoices legacy
                        JOIN platform_invoices current ON current.payer_id = legacy.owner_id
                            AND current.period_year = EXTRACT(YEAR FROM legacy.period_start)::int
                            AND current.period_month = EXTRACT(MONTH FROM legacy.period_start)::int
                        WHERE current.total_fee_amount <> legacy.amount
                    ) THEN
                        RAISE EXCEPTION 'Hoa don legacy va hoa don goc cung ky khac so tien';
                    END IF;
                END $$;

                INSERT INTO platform_invoices
                    (id, payer_id, period_year, period_month, total_fee_amount, status,
                     created_at, payment_proof_url, submitted_at)
                SELECT id, owner_id,
                    EXTRACT(YEAR FROM period_start)::int,
                    EXTRACT(MONTH FROM period_start)::int,
                    amount,
                    CASE WHEN status = 'UNPAID' THEN 'PENDING' ELSE status END,
                    created_at, payment_proof_url, submitted_at
                FROM platform_fee_invoices
                ON CONFLICT (payer_id, period_year, period_month) DO UPDATE SET
                    payment_proof_url = COALESCE(platform_invoices.payment_proof_url, EXCLUDED.payment_proof_url),
                    submitted_at = COALESCE(platform_invoices.submitted_at, EXCLUDED.submitted_at),
                    status = CASE
                        WHEN platform_invoices.status = 'PAID' OR EXCLUDED.status = 'PAID' THEN 'PAID'
                        WHEN platform_invoices.status = 'SUBMITTED' OR EXCLUDED.status = 'SUBMITTED' THEN 'SUBMITTED'
                        ELSE platform_invoices.status
                    END;
                """);

            migrationBuilder.DropTable(
                name: "platform_fee_invoices");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "platform_fee_invoices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    payment_proof_url = table.Column<string>(type: "text", nullable: true),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
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

            migrationBuilder.CreateIndex(
                name: "IX_platform_fee_invoices_owner_id_period_start",
                table: "platform_fee_invoices",
                columns: new[] { "owner_id", "period_start" },
                unique: true);
        }
    }
}
