START TRANSACTION;

CREATE SEQUENCE depot_batch_number START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE NO CYCLE;

ALTER TABLE inventory_batches ADD code character varying(40);

CREATE UNIQUE INDEX "IX_inventory_batches_code" ON inventory_batches (code);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260924133220_DepotBatchCodes', '8.0.10');

COMMIT;

