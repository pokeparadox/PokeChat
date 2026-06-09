using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Data.Sqlite;

namespace PokeChat.Data;

public class DatabaseInitializer(PokeChatDbContext context)
{
    public void Initialize()
    {
        try
        {
            var pending = context.Database.GetPendingMigrations().ToList();
            if (pending.Count > 0)
                context.Database.Migrate();
        }
        catch (SqliteException)
        {
            // __EFMigrationsHistory table doesn't exist yet.
            // Determine if this is a legacy DB (from EnsureCreated) or a fresh DB.
            if (HasTables())
            {
                // Legacy DB: seed migration history then apply any new migrations
                SeedMigrationHistory();
            }

            context.Database.Migrate();
        }

        DbSeeder.Seed(context);
    }

    private bool HasTables()
    {
        try
        {
            var count = (int)context.Database.ExecuteSqlRaw(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name != '__EFMigrationsHistory'");
            return count > 0;
        }
        catch
        {
            return false;
        }
    }

    private void SeedMigrationHistory()
    {
        var migrationsAssembly = ((IInfrastructure<IServiceProvider>)context.Database).GetService<IMigrationsAssembly>();
        var allMigrations = migrationsAssembly.Migrations.Keys.ToList();
        if (allMigrations.Count == 0) return;

        context.Database.ExecuteSqlRaw(
            "CREATE TABLE \"__EFMigrationsHistory\" (\"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, \"ProductVersion\" TEXT NOT NULL)");

        foreach (var migrationId in allMigrations)
        {
            context.Database.ExecuteSqlRaw(
                "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({0}, {1})",
                migrationId, "10.0.0");
        }
    }
}
