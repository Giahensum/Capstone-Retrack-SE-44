import { execFileSync } from 'node:child_process';

// Chỉ thay đoạn giao hàng đang chờ TV3. Không phải kiểm thử Driver E2E.
export function deliverTestBatch(batchId) {
  if (!['Retrack_TV2_dev', 'Retrack_TV2_test'].includes(process.env.PGDATABASE)
    || !['localhost', '127.0.0.1'].includes(process.env.PGHOST)
    || process.env.E2E_TRANSPORT_FIXTURE !== 'true') {
    throw new Error('Fixture chỉ được chạy với database local Retrack_TV2_dev/test và cờ E2E_TRANSPORT_FIXTURE=true.');
  }
  if (!/^[0-9a-f-]{36}$/i.test(batchId)) throw new Error('Mã lô fixture không hợp lệ.');
  execFileSync(process.env.E2E_PSQL_PATH || 'psql', ['-X', '-v', 'ON_ERROR_STOP=1'], {
    encoding: 'utf8',
    input: `DO $$
      DECLARE fixture_depot uuid; affected integer;
      BEGIN
        SELECT depot_id INTO fixture_depot FROM inventory_batches
          WHERE id = '${batchId}'::uuid AND description LIKE 'E2E-Depot-Factory-%';
        IF fixture_depot IS NULL THEN RAISE EXCEPTION 'Không phải lô fixture E2E'; END IF;
        PERFORM 1 FROM depots WHERE id = fixture_depot FOR UPDATE;
        UPDATE transport_jobs SET status = 'DELIVERED', updated_at = now()
          WHERE batch_id = '${batchId}'::uuid AND status = 'PENDING';
        GET DIAGNOSTICS affected = ROW_COUNT;
        IF affected <> 1 THEN RAISE EXCEPTION 'Fixture cần đúng một chuyến PENDING'; END IF;
      END $$;`,
    stdio: ['pipe', 'pipe', 'pipe'],
  });
}
