using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
            Console.WriteLine("[Database] Migration failed. Deleting DB and recreating...");
            RecreateDatabase(dbPath);

            if (File.Exists(bakPath))
            {
                try
                {
                    Console.WriteLine("[Database] Restoring learned data from backup...");
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

    private void RecreateDatabase(string dbPath)
    {
        SqliteConnection.ClearAllPools();

        try { File.Delete(dbPath); }
        catch (Exception ex) { Console.WriteLine($"[Database] Could not delete DB file: {ex.Message}"); }

        try
        {
            context.Database.Migrate();
        }
        catch
        {
            Console.WriteLine("[Database] Migrate failed on fresh DB. Falling back to EnsureCreated...");
            try { context.Database.EnsureCreated(); }
            catch (Exception ex) { Console.WriteLine($"[Database] EnsureCreated also failed: {ex.Message}"); }
        }
    }

    public static string ResolveDbPath()
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
