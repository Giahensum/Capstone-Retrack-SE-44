using Microsoft.EntityFrameworkCore;
using Retrack.API.DTOs.Depot;

namespace Retrack.API.Repositories;

public sealed partial class DepotReportRepository
{
    public Task<List<CostChannelDto>> CostChannelsAsync(Guid depotId, DateTime start, DateTime end)
    {
        // Tổng hợp tại PostgreSQL trước join để không nhân đôi nguồn vốn khi có nhiều khoản chi.
        // SqlQuery nội suy của EF tham số hóa toàn bộ giá trị đầu vào.
        return db.Database.SqlQuery<CostChannelDto>($"""
            WITH sources AS (
                SELECT s.batch_id, SUM(s.quantity) AS quantity, COUNT(*) FILTER (WHERE s.unit_cost IS NULL) AS missing,
                       SUM(s.quantity * s.unit_cost) AS cost
                FROM depot_batch_cost_sources s JOIN inventory_batches b ON b.id = s.batch_id
                WHERE b.depot_id = {depotId} GROUP BY s.batch_id
            ), expenses AS (
                SELECT e.batch_id, SUM(e.amount) AS amount FROM depot_batch_expenses e
                JOIN inventory_batches b ON b.id = e.batch_id WHERE b.depot_id = {depotId} GROUP BY e.batch_id
            ), rows AS (
                SELECT 'FACTORY' AS channel,
                    CASE WHEN b.settled_at IS NOT NULL THEN b.gross_amount ELSE q.gross_amount END AS gross,
                    CASE WHEN b.settled_at IS NOT NULL THEN b.platform_fee_amount ELSE q.platform_fee_amount END AS fee,
                    CASE WHEN b.settled_at IS NOT NULL THEN b.net_amount ELSE q.net_amount END AS net,
                    CASE WHEN s.missing = 0 AND s.quantity = b.declared_weight_kg THEN ROUND(s.cost, 0) ELSE NULL END AS cost,
                    COALESCE(e.amount, 0) AS selling,
                    EXISTS(SELECT 1 FROM depot_batch_cost_sources cs JOIN depot_stock_cost_adjustments a ON a.line_id = cs.source_id
                           WHERE cs.batch_id = b.id AND cs.source_type = 'RECEIPT' AND a.created_at > b.created_at) AS needs_review
                FROM inventory_batches b
                LEFT JOIN sources s ON s.batch_id = b.id LEFT JOIN expenses e ON e.batch_id = b.id
                LEFT JOIN batch_quality_checks q ON q.batch_id = b.id
                WHERE b.depot_id = {depotId}
                    AND ((b.settled_at IS NOT NULL AND b.status IN ('COMPLETED', 'PAID')) OR
                         (b.settled_at IS NULL AND b.status NOT IN ('CANCELLED', 'REJECTED') AND q.is_accepted AND q.payment_proof_url IS NOT NULL))
                    AND COALESCE(b.settled_at, q.created_at) >= {start} AND COALESCE(b.settled_at, q.created_at) < {end}
                UNION ALL
                SELECT 'REUSABLE', x.quantity * x.unit_price, 0, x.quantity * x.unit_price,
                       x.unit_cost * x.quantity, x.selling_cost,
                       EXISTS(SELECT 1 FROM depot_stock_cost_adjustments a WHERE a.line_id = x.line_id AND a.created_at > x.created_at)
                FROM depot_reuse_sales x JOIN depot_stock_lines l ON l.id = x.line_id
                JOIN depot_stock_receipts r ON r.id = l.receipt_id
                WHERE r.depot_id = {depotId} AND x.created_at >= {start} AND x.created_at < {end}
            )
            SELECT c.channel AS "Channel", COUNT(r.channel)::int AS "TransactionCount",
                (COUNT(*) FILTER (WHERE r.channel IS NOT NULL AND (r.cost IS NULL OR r.net IS NULL OR r.needs_review)))::int AS "IncompleteCount",
                CASE WHEN COUNT(*) FILTER (WHERE r.channel IS NOT NULL AND r.gross IS NULL) > 0 THEN NULL ELSE COALESCE(SUM(r.gross), 0) END AS "GrossRevenue",
                CASE WHEN COUNT(*) FILTER (WHERE r.channel IS NOT NULL AND r.fee IS NULL) > 0 THEN NULL ELSE COALESCE(SUM(r.fee), 0) END AS "PlatformFees",
                CASE WHEN COUNT(*) FILTER (WHERE r.channel IS NOT NULL AND r.net IS NULL) > 0 THEN NULL ELSE COALESCE(SUM(r.net), 0) END AS "NetRevenue",
                COALESCE(SUM(r.cost), 0) AS "KnownCostOfGoods", COALESCE(SUM(r.selling), 0) AS "SellingCosts",
                CASE WHEN COUNT(*) FILTER (WHERE r.channel IS NOT NULL AND (r.cost IS NULL OR r.net IS NULL OR r.needs_review)) > 0 THEN NULL
                     ELSE COALESCE(SUM(r.net - r.cost - r.selling), 0) END AS "PretaxDifference"
            FROM (VALUES ('FACTORY'), ('REUSABLE')) AS c(channel) LEFT JOIN rows r ON r.channel = c.channel
            GROUP BY c.channel ORDER BY c.channel
            """).ToListAsync();
    }
}
