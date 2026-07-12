using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Data.Sqlite;

namespace PokeChat.Data;

public class DatabaseInitializer(PokeChatDbContext context)
{
    public void Initialize()
    {
        var dbPath = ResolveDbPath();
        var bakPath = BackupHelper.GetBackupPath(dbPath);

        BackupHelper.Backup(dbPath);

        try
        {
            context.Database.Migrate();
        }
        catch
        {
            Console.WriteLine("[Database] Migration failed. Wiping and re-migrating...");
            WipeAllTables(dbPath);

            try
            {
                context.Database.Migrate();
            }
            catch
            {
                Console.WriteLine("[Database] Re-migration also failed. Creating schema from model...");
                context.Database.EnsureCreated();
            }

            if (File.Exists(bakPath))
            {
                try
                {
                    Console.WriteLine("[Database] Copying learned data from backup...");
                    BackupHelper.CopyData(bakPath, dbPath);
                    Console.WriteLine("[Database] Learned data restored.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Database] Backup restore failed: {ex.Message}");
                }
            }
        }

        DbSeeder.Seed(context);
    }

    private void WipeAllTables(string dbPath)
    {
        context.Database.CloseConnection();
        SqliteConnection.ClearAllPools();

        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();

        using var getTables = conn.CreateCommand();
        getTables.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
        var tables = new List<string>();
        using (var reader = getTables.ExecuteReader())
        {
            while (reader.Read())
                tables.Add(reader.GetString(0));
        }

        using var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF";
        pragma.ExecuteNonQuery();

        foreach (var table in tables)
        {
            using var drop = conn.CreateCommand();
            drop.CommandText = $"DROP TABLE IF EXISTS \"{table}\"";
            drop.ExecuteNonQuery();
        }

        using var pragmaOn = conn.CreateCommand();
        pragmaOn.CommandText = "PRAGMA foreign_keys = ON";
        pragmaOn.ExecuteNonQuery();

        conn.Close();
    }

    private static string ResolveDbPath()
    {
        var envPath = Environment.GetEnvironmentVariable("POKECHAT_DB_PATH");
        if (!string.IsNullOrEmpty(envPath))
            return envPath;

        var baseDir = AppContext.BaseDirectory;
        var root = ProjectPathHelper.FindProjectRoot(baseDir);
        if (root != null)
            return Path.Combine(root, "pokechat.db");

        return Path.Combine(baseDir, "pokechat.db");
    }
}
