using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PokeChat.Data;

namespace PokeChat.Tests.Helpers;

public class FreshDbContext : IDisposable
{
    private readonly SqliteConnection _connection;

    public FreshDbContext()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<PokeChatDbContext>()
            .UseSqlite(_connection)
            .Options;
        Context = new PokeChatDbContext(options);
        Context.Database.EnsureCreated();
    }

    public PokeChatDbContext Context { get; }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
