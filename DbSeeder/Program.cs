using System;
using System.IO;
using Npgsql;

class Program
{
    static void Main(string[] args)
    {
        string serverConnStr = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=123";
        string dbName = "ReTrack_DB";
        string targetConnStr = $"Host=localhost;Port=5432;Database={dbName};Username=postgres;Password=123";

        if (args.Length >= 2 && args[0] == "get-otp")
        {
            using var otpConn = new NpgsqlConnection(targetConnStr);
            otpConn.Open();
            using var cmd = new NpgsqlCommand($"SELECT otp_code FROM password_reset_otps WHERE email = '{args[1]}' AND is_used = false ORDER BY created_at DESC LIMIT 1;", otpConn);
            var otp = cmd.ExecuteScalar();
            Console.WriteLine(otp ?? "");
            return;
        }

        if (args.Length >= 1 && args[0] == "check-depots")
        {
            using var dConn = new NpgsqlConnection(targetConnStr);
            dConn.Open();
            // Cập nhật trạng thái duyệt cho các kho demo theo đúng retrack-system.sql dòng 795
            using (var updCmd = new NpgsqlCommand(@"
                UPDATE depots SET verification_status = 'APPROVED'
                WHERE id IN ('00000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000012')
                  AND owner_id = '00000000-0000-0000-0000-000000000003';", dConn))
            {
                var affected = updCmd.ExecuteNonQuery();
                Console.WriteLine($"Approved demo depots: {affected} rows updated.");
            }

            using var cmd = new NpgsqlCommand(@"
                SELECT d.id, d.name, d.verification_status, d.latitude, d.longitude, u.full_name, u.is_active 
                FROM depots d 
                JOIN users u ON d.owner_id = u.id;", dConn);
            using var r = cmd.ExecuteReader();
            Console.WriteLine("=== DEPOTS IN DATABASE ===");
            while (r.Read())
            {
                Console.WriteLine($"Name: '{r["name"]}', Status: '{r["verification_status"]}', Owner: '{r["full_name"]}', OwnerActive: {r["is_active"]}, Lat: {r["latitude"]}, Lng: {r["longitude"]}");
            }
            return;
        }

        Console.WriteLine($"Checking if database '{dbName}' exists...");
        using (var serverConn = new NpgsqlConnection(serverConnStr))
        {
            serverConn.Open();
            using var checkCmd = new NpgsqlCommand($"SELECT 1 FROM pg_database WHERE datname = '{dbName}';", serverConn);
            var exists = checkCmd.ExecuteScalar() != null;
            if (!exists)
            {
                Console.WriteLine($"Database '{dbName}' does not exist. Creating it now...");
                using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{dbName}\";", serverConn);
                createCmd.ExecuteNonQuery();
                Console.WriteLine($"Database '{dbName}' created successfully!");
            }
            else
            {
                Console.WriteLine($"Database '{dbName}' already exists.");
            }
        }

        Console.WriteLine($"\nConnecting to target database '{dbName}'...");
        using var conn = new NpgsqlConnection(targetConnStr);
        conn.Open();
        Console.WriteLine("Connected to target database successfully!");

        string rootDir = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(rootDir) && !File.Exists(Path.Combine(rootDir, "backend", "Retrack.sln")))
        {
            var parent = Directory.GetParent(rootDir)?.FullName;
            if (parent == rootDir) break;
            rootDir = parent ?? "";
        }

        string newSqlPath = Path.Combine(rootDir, "db", "bootstrap", "retrack-system.sql");
        if (!File.Exists(newSqlPath))
        {
            Console.WriteLine($"Error: File not found at {newSqlPath}");
            return;
        }

        Console.WriteLine($"\nReading {newSqlPath}...");
        string fullSql = File.ReadAllText(newSqlPath);

        // Chế độ nâng cấp schema: chỉ chạy khối schema trước -- RETRACK_SCHEMA_END để bảo toàn dữ liệu cũ
        int schemaEndIdx = fullSql.IndexOf("-- RETRACK_SCHEMA_END", StringComparison.OrdinalIgnoreCase);
        string schemaSql = schemaEndIdx > 0 ? fullSql.Substring(0, schemaEndIdx) : fullSql;
        if (!schemaSql.Contains("COMMIT;", StringComparison.OrdinalIgnoreCase))
        {
            schemaSql += "\nCOMMIT;\n";
        }

        Console.WriteLine($"Executing schema migration ({schemaSql.Length} bytes)...");
        using (var cmd = new NpgsqlCommand(schemaSql, conn))
        {
            cmd.CommandTimeout = 180;
            cmd.ExecuteNonQuery();
        }
        Console.WriteLine("-> Schema updated and COMMITTED successfully!");
        conn.Close();

        // Mở kết nối mới hoàn toàn để kiểm chứng các bảng đã được lưu vĩnh viễn (persisted)
        Console.WriteLine("\nVerifying persistence with a new connection...");
        using var verifyConn = new NpgsqlConnection(targetConnStr);
        verifyConn.Open();

        Console.WriteLine("\n=== VERIFICATION STATS ===");
        string[] tables = new[]
        {
            "users",
            "depots",
            "factories",
            "pickup_requests",
            "depot_stock_receipts",
            "liquidation_lots",
            "password_reset_otps",
            "notifications"
        };

        foreach (var table in tables)
        {
            try
            {
                using var countCmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {table};", verifyConn);
                var count = countCmd.ExecuteScalar();
                Console.WriteLine($"  - {table}: {count} records");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  - {table}: Error ({ex.Message})");
            }
        }

        Console.WriteLine("\n=== DATABASE UPDATE COMPLETED SAFELY! ===\n");
    }
}
