using Microsoft.EntityFrameworkCore;
using PokeChat.Data.Entities;

namespace PokeChat.Data;

public sealed class PokeChatDbContext : DbContext
{
    private readonly string? _dbPath;

    public PokeChatDbContext(string? dbPath = null)
    {
        _dbPath = dbPath ?? ResolveDbPath();
        Database.EnsureCreated();
    }

    public PokeChatDbContext(DbContextOptions<PokeChatDbContext> options) : base(options)
    {
    }

    private static string ResolveDbPath()
    {
        var baseDir = AppContext.BaseDirectory;
        var path = Path.Combine(baseDir, "pokechat.db");

        var current = baseDir;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "PokeChat.csproj")))
            {
                return Path.Combine(current, "pokechat.db");
            }
            current = Path.GetDirectoryName(current);
        }

        return path;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<FactEntity> Facts => Set<FactEntity>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Greeting> Greetings => Set<Greeting>();
    public DbSet<GreetingWord> GreetingWords => Set<GreetingWord>();
    public DbSet<ResponseRule> ResponseRules => Set<ResponseRule>();
    public DbSet<ResponseRuleResponse> ResponseRuleResponses => Set<ResponseRuleResponse>();
    public DbSet<PosDictionaryEntry> PosDictionary => Set<PosDictionaryEntry>();
    public DbSet<NamePattern> NamePatterns => Set<NamePattern>();
    public DbSet<BotCommand> BotCommands => Set<BotCommand>();
    public DbSet<Misspelling> Misspellings => Set<Misspelling>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite($"Data Source={_dbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.FirstSeen).IsRequired();
            entity.Property(e => e.LastSeen).IsRequired();
        });

        modelBuilder.Entity<FactEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Subject).IsRequired();
            entity.Property(e => e.Verb).IsRequired();
            entity.Property(e => e.Object).IsRequired();
            entity.Property(e => e.PredicateType).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId);
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserInput).IsRequired();
            entity.Property(e => e.BotResponse).IsRequired();
            entity.Property(e => e.Timestamp).IsRequired();
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId);
        });

        modelBuilder.Entity<Greeting>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Text).IsRequired();
            entity.Property(e => e.IsSystem).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<GreetingWord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Word).IsUnique();
            entity.Property(e => e.Word).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.LearnedFromUserId);
        });

        modelBuilder.Entity<ResponseRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Pattern).IsRequired();
            entity.Property(e => e.InputType).IsRequired();
            entity.Property(e => e.IsActive).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasMany(e => e.Responses)
                .WithOne(e => e.Rule)
                .HasForeignKey(e => e.RuleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ResponseRuleResponse>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ResponseText).IsRequired();
            entity.HasOne(e => e.Rule)
                .WithMany(e => e.Responses)
                .HasForeignKey(e => e.RuleId);
        });

        modelBuilder.Entity<PosDictionaryEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Word).IsRequired();
            entity.Property(e => e.WordType).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<NamePattern>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Pattern).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<BotCommand>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Command).IsUnique();
            entity.Property(e => e.Command).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<Misspelling>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.WrongWord).IsUnique();
            entity.Property(e => e.WrongWord).IsRequired();
            entity.Property(e => e.Correction).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
        });
    }
}
