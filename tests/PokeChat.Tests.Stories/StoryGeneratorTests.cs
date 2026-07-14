using PokeChat.Knowledge;
using PokeChat.Stories;
using PokeChat.Tests.Shared.Helpers;
using Shouldly;

namespace PokeChat.Tests.Stories;

public class StoryGeneratorTests
{
    [Fact]
    public void GenerateStory_ReturnsNonEmptyString()
    {
        using var db = new FreshDbContext();
        TestDataHelper.SeedPosDictionary(db.Context);
        TestDataHelper.SeedStoryTemplates(db.Context);
        var store = new KnowledgeStore(db.Context);
        var generator = new StoryGenerator(store);

        var story = generator.GenerateStory();

        story.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateStory_ResolvesNounSlot()
    {
        using var db = new FreshDbContext();
        TestDataHelper.SeedPosDictionary(db.Context);
        TestDataHelper.SeedStoryTemplates(db.Context);
        var store = new KnowledgeStore(db.Context);
        var generator = new StoryGenerator(store);

        var story = generator.GenerateStory();

        story.ShouldNotContain("{noun}");
    }

    [Fact]
    public void GenerateStory_ResolvesUserSlot()
    {
        using var db = new FreshDbContext();
        TestDataHelper.SeedPosDictionary(db.Context);
        TestDataHelper.SeedStoryTemplates(db.Context);
        var store = new KnowledgeStore(db.Context);
        var generator = new StoryGenerator(store);

        var story = generator.GenerateStory(userName: "Alice");

        story.ShouldNotContain("{user}");
    }

    [Fact]
    public void GenerateStory_ResolvesUserLikeSlot_FromUserFacts()
    {
        using var db = new FreshDbContext();
        TestDataHelper.SeedPosDictionary(db.Context);
        TestDataHelper.SeedStoryTemplates(db.Context);
        var store = new KnowledgeStore(db.Context);
        var userId = store.GetOrCreateUser("Alice");
        store.StoreFact(new Fact
        {
            UserId = userId,
            Subject = "Alice",
            Verb = "likes",
            Object = "pizza",
            PredicateType = "Preference",
            CreatedAt = DateTime.UtcNow.ToString("o")
        });
        store.Save();

        var generator = new StoryGenerator(store);
        var story = generator.GenerateStory(userName: "Alice", userId: userId);

        story.ShouldNotContain("{user_like}");
    }

    [Fact]
    public void GenerateStory_FallsBackWhenNoUserFacts()
    {
        using var db = new FreshDbContext();
        TestDataHelper.SeedPosDictionary(db.Context);
        TestDataHelper.SeedStoryTemplates(db.Context);
        var store = new KnowledgeStore(db.Context);
        var userId = store.GetOrCreateUser("Bob");

        var generator = new StoryGenerator(store);
        var story = generator.GenerateStory(userName: "Bob", userId: userId);

        story.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateStory_MultipleSlots_AllResolved()
    {
        using var db = new FreshDbContext();
        TestDataHelper.SeedPosDictionary(db.Context);
        TestDataHelper.SeedStoryTemplates(db.Context);
        var store = new KnowledgeStore(db.Context);
        var generator = new StoryGenerator(store);

        var story = generator.GenerateStory(userName: "Charlie");

        story.ShouldNotContain("{");
    }
}
