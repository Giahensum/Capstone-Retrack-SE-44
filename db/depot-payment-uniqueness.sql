START TRANSACTION;

CREATE UNIQUE INDEX "IX_platform_transactions_source_type_source_id" ON platform_transactions (source_type, source_id);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260924064439_DepotPaymentUniqueness', '8.0.10');

COMMIT;

