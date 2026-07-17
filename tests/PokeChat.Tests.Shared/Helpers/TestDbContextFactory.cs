using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PokeChat.Data;

namespace PokeChat.Tests.Shared.Helpers;

public sealed class TestDbContextFactory : IDbContextFactory<PokeChatDbContext>, IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDbContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public PokeChatDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PokeChatDbContext>()
            .UseSqlite(_connection)
            .Options;
        var ctx = new PokeChatDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    public void Dispose() => _connection.Dispose();
}
