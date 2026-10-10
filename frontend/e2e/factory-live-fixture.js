import { execFileSync } from "node:child_process";
import { randomUUID } from "node:crypto";

const allowedDatabases = ["Retrack_TV2_test", "Retrack_Factory_local"];

function assertSafeFixtureEnvironment() {
  if (process.env.E2E_FACTORY_DB_FIXTURE !== "true"
    || !allowedDatabases.includes(process.env.PGDATABASE)
    || !["localhost", "127.0.0.1"].includes(process.env.PGHOST)) {
    throw new Error("Fixture Factory chỉ được chạy trên database local được phép với E2E_FACTORY_DB_FIXTURE=true.");
  }
}

function psql(sql) {
  assertSafeFixtureEnvironment();
  execFileSync(process.env.E2E_PSQL_PATH || "psql", ["-X", "-v", "ON_ERROR_STOP=1"], {
    encoding: "utf8",
    input: sql,
    stdio: ["pipe", "pipe", "pipe"],
  });
}

export function createDeliveredFactoryOrder() {
  const batchId = randomUUID();
  const jobId = randomUUID();
  const code = `E2E-FAC-${Date.now()}`;
  psql(`DO $$
    DECLARE fixture_factory uuid; fixture_depot uuid;
    BEGIN
      SELECT f.id INTO fixture_factory FROM factories f
        JOIN users u ON u.id = f.owner_id
        WHERE u.email = 'factory@retrack.vn' AND u.role = 'FACTORY' AND u.is_active;
      SELECT d.id INTO fixture_depot FROM depots d
        JOIN users u ON u.id = d.owner_id
        WHERE u.role = 'DEPOT_OWNER' AND u.is_active
        ORDER BY d.created_at, d.id LIMIT 1;
      IF fixture_factory IS NULL OR fixture_depot IS NULL THEN
        RAISE EXCEPTION 'Thiếu Factory hoặc Depot demo đang hoạt động';
      END IF;
      PERFORM 1 FROM depots WHERE id = fixture_depot FOR UPDATE;
      INSERT INTO inventory_batches
        (id, code, depot_id, target_factory_id, material_type, declared_weight_kg,
         description, status, created_at, updated_at)
      VALUES
        ('${batchId}'::uuid, '${code}', fixture_depot, fixture_factory, 'PET', 100,
         'E2E-Factory-Live-${batchId}', 'ACCEPTED', now(), now());
      INSERT INTO transport_jobs (id, batch_id, status, created_at, updated_at)
      VALUES ('${jobId}'::uuid, '${batchId}'::uuid, 'DELIVERED', now(), now());
    END $$;`);
  return { batchId, code };
}

export function cleanupDeliveredFactoryOrder(batchId) {
  if (!/^[0-9a-f-]{36}$/i.test(batchId)) throw new Error("Mã lô fixture không hợp lệ.");
  psql(`DO $$
    DECLARE fixture_depot uuid;
    BEGIN
      SELECT depot_id INTO fixture_depot FROM inventory_batches
        WHERE id = '${batchId}'::uuid AND description = 'E2E-Factory-Live-${batchId}';
      IF fixture_depot IS NULL THEN RETURN; END IF;
      PERFORM 1 FROM depots WHERE id = fixture_depot FOR UPDATE;
      DELETE FROM platform_transactions WHERE source_type = 'BATCH_ORDER' AND source_id = '${batchId}'::uuid;
      DELETE FROM factory_depot_reviews WHERE batch_id = '${batchId}'::uuid;
      DELETE FROM batch_quality_checks WHERE batch_id = '${batchId}'::uuid;
      DELETE FROM transport_jobs WHERE batch_id = '${batchId}'::uuid;
      DELETE FROM inventory_batches
        WHERE id = '${batchId}'::uuid AND description = 'E2E-Factory-Live-${batchId}';
    END $$;`);
}
