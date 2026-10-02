# TV4 migration rehearsal

Run this rehearsal against a disposable PostgreSQL database that contains data from the previous migration, never directly against production.

1. Restore a recent backup into a temporary database and record row counts for `users`, `factories`, `inventory_batches`, `batch_quality_checks`, `transport_jobs`, and `factory_depot_partnerships`.
2. Set `ConnectionStrings__DefaultConnection` to that database.
3. Start the API in Development. `Program.cs` executes `Database.Migrate()` before seeding.
4. Confirm the two Factory migrations are applied in `__EFMigrationsHistory`.
5. Repeat the row-count checks and exercise one existing batch through the read-only Factory dashboard.
6. Abort if any existing row count changes unexpectedly or if a migration fails; capture the migration log as the deployment evidence.

The repository does not claim this rehearsal passed until it is run with a real pre-existing database and the required Factory credentials.
