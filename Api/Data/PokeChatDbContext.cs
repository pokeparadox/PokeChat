using Microsoft.EntityFrameworkCore;
using PokeChat.Data.Entities;

namespace PokeChat.Data;

public sealed class PokeChatDbContext : DbContext
{
    public PokeChatDbContext(DbContextOptions<PokeChatDbContext> options) : base(options)
    {
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
    public DbSet<BotResponse> BotResponses => Set<BotResponse>();
    public DbSet<WordDefinition> WordDefinitions => Set<WordDefinition>();
    public DbSet<WordLink> WordLinks => Set<WordLink>();
    public DbSet<NounCategory> NounCategories => Set<NounCategory>();
    public DbSet<UserBotName> UserBotNames => Set<UserBotName>();
    public DbSet<BotRenamePattern> BotRenamePatterns => Set<BotRenamePattern>();
    public DbSet<EmotionKeyword> EmotionKeywords => Set<EmotionKeyword>();
    public DbSet<ContractionEntity> Contractions => Set<ContractionEntity>();
    public DbSet<TemporalExpression> TemporalExpressions => Set<TemporalExpression>();
    public DbSet<ConversationSession> ConversationSessions => Set<ConversationSession>();
    public DbSet<LearnedResponseRule> LearnedResponseRules => Set<LearnedResponseRule>();
    public DbSet<ResponseFeedback> ResponseFeedbacks => Set<ResponseFeedback>();
    public DbSet<TurnRating> TurnRates => Set<TurnRating>();
    public DbSet<ConversationMetric> ConversationMetrics => Set<ConversationMetric>();
    public DbSet<ResponseEffectiveness> ResponseEffectiveness => Set<ResponseEffectiveness>();
    public DbSet<StoryTemplate> StoryTemplates => Set<StoryTemplate>();
    public DbSet<MadLibTemplate> MadLibTemplates => Set<MadLibTemplate>();
    public DbSet<Joke> Jokes => Set<Joke>();
    public DbSet<Riddle> Riddles => Set<Riddle>();
    public DbSet<RhymeGroup> RhymeGroups => Set<RhymeGroup>();
    public DbSet<PoemTemplate> PoemTemplates => Set<PoemTemplate>();
    public DbSet<ErrorKnowledgeEntry> ErrorKnowledgeEntries => Set<ErrorKnowledgeEntry>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<FactEndorsement> FactEndorsements => Set<FactEndorsement>();
    public DbSet<AllowedCommand> AllowedCommands => Set<AllowedCommand>();

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
            entity.Property(e => e.Sentiment);
            entity.Property(e => e.EmotionIntensity);
            entity.Property(e => e.TimeContext);
            entity.Property(e => e.MentionedAt);
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
            entity.Property(e => e.SessionId);
            entity.Property(e => e.ResponseCategory);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId);
        });

        modelBuilder.Entity<Greeting>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Text).IsRequired();
            entity.Property(e => e.IsSystem).IsRequired();
            entity.Property(e => e.Persona);
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
            entity.Property(e => e.Persona);
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

        modelBuilder.Entity<BotResponse>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Category).IsRequired();
            entity.Property(e => e.ResponseText).IsRequired();
            entity.Property(e => e.Persona);
            entity.Property(e => e.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<WordDefinition>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Word).IsRequired();
            entity.Property(e => e.Definition).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.DefinedByUserId);
        });

        modelBuilder.Entity<WordLink>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SourceWord).IsRequired();
            entity.Property(e => e.TargetWord).IsRequired();
            entity.Property(e => e.LinkType).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId);
        });

        modelBuilder.Entity<NounCategory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Noun).IsUnique();
            entity.Property(e => e.Noun).IsRequired();
            entity.Property(e => e.Category).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.LearnedFromUserId);
        });

        modelBuilder.Entity<UserBotName>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.Property(e => e.BotName).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId);
        });

        modelBuilder.Entity<BotRenamePattern>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Pattern).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<EmotionKeyword>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Word).IsUnique();
            entity.Property(e => e.Word).IsRequired();
            entity.Property(e => e.Sentiment).IsRequired();
            entity.Property(e => e.Intensity);
            entity.Property(e => e.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<ContractionEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Contraction).IsUnique();
            entity.Property(e => e.Contraction).IsRequired();
            entity.Property(e => e.Expansion).IsRequired();
        });

        modelBuilder.Entity<TemporalExpression>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Expression).IsUnique();
            entity.Property(e => e.Expression).IsRequired();
            entity.Property(e => e.DaysOffset);
            entity.Property(e => e.IsRange);
        });

        modelBuilder.Entity<ConversationSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SessionGuid).IsUnique();
            entity.Property(e => e.SessionGuid).IsRequired();
            entity.Property(e => e.StartedAt).IsRequired();
            entity.Property(e => e.TurnCount);
            entity.Property(e => e.LastActiveAt);
            entity.Property(e => e.BotName);
            entity.Property(e => e.Persona);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId);
        });

        modelBuilder.Entity<LearnedResponseRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Pattern).IsRequired();
            entity.Property(e => e.ResponseTemplate).IsRequired();
            entity.Property(e => e.InputType).IsRequired();
            entity.Property(e => e.Confidence);
            entity.Property(e => e.IsActive);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.LearnedFromUserId);
        });

        modelBuilder.Entity<ResponseFeedback>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RuleId).IsRequired();
            entity.Property(e => e.IsLearnedRule);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.Feedback).IsRequired();
            entity.Property(e => e.CorrectionText);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId);
        });

        modelBuilder.Entity<TurnRating>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Rating).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasOne<Conversation>()
                .WithMany()
                .HasForeignKey(e => e.ConversationId);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId);
        });

        modelBuilder.Entity<ConversationMetric>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SessionId).IsRequired();
            entity.Property(e => e.TurnCount);
            entity.Property(e => e.FactsLearned);
            entity.Property(e => e.DominantSentiment);
            entity.Property(e => e.SentimentTrend);
            entity.Property(e => e.TopicsDiscussed);
            entity.Property(e => e.BotResponseStats);
            entity.Property(e => e.AvgResponseLength);
            entity.Property(e => e.SessionLength);
            entity.Property(e => e.StartedAt).IsRequired();
            entity.Property(e => e.EndedAt).IsRequired();
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId);
        });

        modelBuilder.Entity<ResponseEffectiveness>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Category).IsRequired();
            entity.Property(e => e.AvgSessionLengthAfter);
            entity.Property(e => e.UsedCount);
            entity.Property(e => e.FollowUpRate);
            entity.Property(e => e.LastUsed).IsRequired();
        });

        modelBuilder.Entity<StoryTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Template).IsRequired();
            entity.Property(e => e.Category);
            entity.Property(e => e.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<MadLibTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Template).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<Joke>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Setup).IsRequired();
            entity.Property(e => e.Punchline).IsRequired();
            entity.Property(e => e.Category);
            entity.Property(e => e.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<Riddle>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Question).IsRequired();
            entity.Property(e => e.Answer).IsRequired();
            entity.Property(e => e.Hint);
            entity.Property(e => e.Difficulty);
            entity.Property(e => e.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<RhymeGroup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.RhymeKey, e.Word }).IsUnique();
            entity.Property(e => e.RhymeKey).IsRequired();
            entity.Property(e => e.Word).IsRequired();
            entity.Property(e => e.WordType).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<PoemTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Template).IsRequired();
            entity.Property(e => e.PoemType).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<ErrorKnowledgeEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Pattern).IsRequired();
            entity.Property(e => e.Suggestion).IsRequired();
            entity.Property(e => e.Language).IsRequired();
            entity.Property(e => e.IsLearned);
            entity.Property(e => e.UsedCount);
            entity.Property(e => e.SuccessCount);
            entity.Property(e => e.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<Reminder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.Task).IsRequired();
            entity.Property(e => e.DueAt).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId);
        });

        modelBuilder.Entity<FactEndorsement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FactId).IsRequired();
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasOne<FactEntity>()
                .WithMany()
                .HasForeignKey(e => e.FactId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId);
        });

        modelBuilder.Entity<AllowedCommand>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Command).IsUnique();
            entity.Property(e => e.Command).IsRequired();
            entity.Property(e => e.IsPermanent).IsRequired();
            entity.Property(e => e.ExpiresAt);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.AddedByUserId);
        });
    }
}
