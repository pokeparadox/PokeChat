using System.Data;
using Microsoft.EntityFrameworkCore;
using PokeChat.Data;
using PokeChat.Tests.Shared.Helpers;
using Shouldly;

namespace PokeChat.Tests.Api;

public class DatabaseSchemaTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    [Fact]
    public void AllExpectedTablesExist()
    {
        using var ctx = _factory.CreateDbContext();
        var conn = ctx.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
                existing.Add(reader.GetString(0));
        }

        var expected = typeof(PokeChatDbContext)
            .GetProperties()
            .Where(p => p.PropertyType.IsGenericType &&
                        p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();

        var missing = expected.Where(t => !existing.Contains(t)).ToList();
        missing.ShouldBeEmpty($"Missing tables: {string.Join(", ", missing)}");
    }

    public void Dispose() => _factory.Dispose();
}
