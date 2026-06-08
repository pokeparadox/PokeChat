using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Data.Sqlite;
using PokeChat.Data.Entities;

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
        SeedMempalaceRules(context);
        SeedMempalaceDictionary(context);
    }

    private static void SeedMempalaceDictionary(PokeChatDbContext context)
    {
        var missingWords = new (string Word, string Type, string NounCategory)[]
        {
            ("memory", "noun", "thing"),
            ("memories", "noun", "thing"),
            ("palace", "noun", "thing"),
            ("knowledge", "noun", "thing"),
            ("facts", "noun", "thing"),
            ("phase", "noun", "thing"),
        };

        foreach (var (word, type, nounCategory) in missingWords)
        {
            if (!context.PosDictionary.Any(e => e.Word == word))
            {
                context.PosDictionary.Add(new PosDictionaryEntry
                {
                    Word = word,
                    WordType = type,
                    CreatedAt = DateTime.UtcNow.ToString("o"),
                });
            }

            if (!context.NounCategories.Any(c => c.Noun == word))
            {
                context.NounCategories.Add(new NounCategory
                {
                    Noun = word,
                    Category = nounCategory,
                    CreatedAt = DateTime.UtcNow.ToString("o"),
                });
            }
        }

        context.SaveChanges();
    }

    private static void SeedMempalaceRules(PokeChatDbContext context)
    {
        var now = DateTime.UtcNow.ToString("o");
        var mempalaceRules = new (string Pattern, string InputType, string[] Responses)[]
        {
            (@"(search|ask|check) (your )?(memory|memories|palace) for (.+)", "Statement", new[]
            {
                "Let me search my memories for that. {tool:mempalace_search:{$4}}",
                "I'll check what I remember about that. {tool:mempalace_search:{$4}}"
            }),
            (@"what do you (remember|know) about (.+)", "Question", new[]
            {
                "Let me check my memories about that. {tool:mempalace_search:{$2}}"
            }),
            (@"(check|query|search) (your )?(facts|knowledge) about (.+)", "Statement", new[]
            {
                "Let me look up what I know about that. {tool:mempalace_search:{$4}}"
            }),
        };

        foreach (var (pattern, inputType, responses) in mempalaceRules)
        {
            var existing = context.ResponseRules.Any(r => r.Pattern == pattern);
            if (existing) continue;

            var rule = new ResponseRule
            {
                Pattern = pattern,
                InputType = inputType,
                IsActive = true,
                CreatedAt = now,
                Responses = responses.Select(r => new ResponseRuleResponse
                {
                    ResponseText = r,
                }).ToList(),
            };
            context.ResponseRules.Add(rule);
        }

        context.SaveChanges();
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
