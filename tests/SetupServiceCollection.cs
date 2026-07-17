using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using PokeChat.Api.Services;
using PokeChat.Data;
using PokeChat.Data.Entities;

var rector = new ServiceCollection();

var dbContextOptions = new DbContextOptionsBuilder<PokeChatDbContext>()
    .UseSqlite("Data Source=./test.db")
    .Options;

rector.AddDbContext<PokeChatDbContext>(options => options.UseSqlite("Data Source=./test.db"));

rector.AddDbContext<SessionManager>(options =>
{
    var serviceProvider = serviceProvider.BuildServiceProvider();
    
    // Get a context from the pool for setup
    using (var context = serviceProvider.GetRequiredService<PokeChatDbContext>())
    {
        // Register it so SessionManager can access it
    }
});