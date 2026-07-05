using PokeChat.Core;
using PokeChat.Knowledge;
using Shouldly;

namespace PokeChat.Tests.Knowledge;

public class ContextTrackerTests
{
    [Fact]
    public void SetAndGetContext()
    {
        var tracker = new ContextTracker();
        tracker.SetContext("key1", "value1");
        tracker.GetContext("key1").ShouldBe("value1");
    }

    [Fact]
    public void GetContext_NonexistentKey_ReturnsNull()
    {
        var tracker = new ContextTracker();
        tracker.GetContext("nonexistent").ShouldBeNull();
    }

    [Fact]
    public void SetContext_NullValue_RemovesKey()
    {
        var tracker = new ContextTracker();
        tracker.SetContext("key1", "value1");
        tracker.SetContext("key1", null);
        tracker.GetContext("key1").ShouldBeNull();
    }

    [Fact]
    public void UpdateLastSubject()
    {
        var tracker = new ContextTracker();
        tracker.UpdateLastSubject("Alice");
        tracker.LastSubject.ShouldBe("Alice");
    }

    [Fact]
    public void UpdateLastObject()
    {
        var tracker = new ContextTracker();
        tracker.UpdateLastObject("pizza");
        tracker.LastObject.ShouldBe("pizza");
    }

    [Fact]
    public void ResolvePronoun_It_ReturnsLastObject()
    {
        var tracker = new ContextTracker();
        tracker.UpdateLastObject("pizza");
        tracker.ResolvePronoun("it").ShouldBe("pizza");
    }

    [Fact]
    public void ResolvePronoun_It_NoObject_ReturnsLastSubject()
    {
        var tracker = new ContextTracker();
        tracker.UpdateLastSubject("Alice");
        tracker.ResolvePronoun("it").ShouldBe("Alice");
    }

    [Fact]
    public void ResolvePronoun_He_ReturnsLastSubject()
    {
        var tracker = new ContextTracker();
        tracker.UpdateLastSubject("Bob");
        tracker.ResolvePronoun("he").ShouldBe("Bob");
    }

    [Fact]
    public void ResolvePronoun_She_ReturnsLastSubject()
    {
        var tracker = new ContextTracker();
        tracker.UpdateLastSubject("Alice");
        tracker.ResolvePronoun("she").ShouldBe("Alice");
    }

    [Fact]
    public void ResolvePronoun_Unknown_ReturnsPronoun()
    {
        var tracker = new ContextTracker();
        tracker.ResolvePronoun("whatever").ShouldBe("whatever");
    }

    [Fact]
    public void Clear_ResetsAll()
    {
        var tracker = new ContextTracker();
        tracker.SetContext("key1", "value1");
        tracker.UpdateLastSubject("Alice");
        tracker.UpdateLastObject("pizza");
        tracker.Clear();
        tracker.GetContext("key1").ShouldBeNull();
        tracker.LastSubject.ShouldBeNull();
        tracker.LastObject.ShouldBeNull();
    }

    [Fact]
    public void ResolvePronoun_They_ReturnsLastSubject()
    {
        var tracker = new ContextTracker();
        tracker.UpdateLastSubject("Alice");
        tracker.ResolvePronoun("they").ShouldBe("Alice");
    }

    [Fact]
    public void PushTopic_AddsToStack()
    {
        var tracker = new ContextTracker();
        tracker.PushTopic("Alice", "like", "pizza", "thing", PredicateType.Preference);
        tracker.TopicStack.Count.ShouldBe(1);

        var entry = tracker.TopicStack[0];
        entry.Subject.ShouldBe("Alice");
        entry.Verb.ShouldBe("like");
        entry.Object.ShouldBe("pizza");
        entry.Category.ShouldBe("thing");
        entry.PredicateType.ShouldBe(PredicateType.Preference);
        entry.TurnNumber.ShouldBe(1);
        entry.MentionCount.ShouldBe(1);
    }

    [Fact]
    public void PushTopic_EvictsOldest_WhenFull()
    {
        var tracker = new ContextTracker();
        for (var i = 1; i <= 5; i++)
            tracker.PushTopic($"Subject{i}", "is", $"Object{i}", null, PredicateType.General);

        tracker.TopicStack.Count.ShouldBe(5);

        tracker.PushTopic("New", "is", "New", null, PredicateType.General);
        tracker.TopicStack.Count.ShouldBe(5);
        tracker.GetTopicBySubject("Subject1").ShouldBeNull();
        tracker.GetTopicBySubject("New").ShouldNotBeNull();
    }

    [Fact]
    public void PushTopic_IncrementsMentionCount_OnDuplicate()
    {
        var tracker = new ContextTracker();
        tracker.PushTopic("Alice", "like", "pizza", "thing", PredicateType.Preference);
        tracker.PushTopic("Alice", "like", "pizza", "thing", PredicateType.Preference);

        tracker.TopicStack.Count.ShouldBe(1);
        tracker.TopicStack[0].MentionCount.ShouldBe(2);
        tracker.TopicStack[0].TurnNumber.ShouldBe(2);
    }

    [Fact]
    public void GetRecentTopics_ReturnsCorrectCount()
    {
        var tracker = new ContextTracker();
        tracker.PushTopic("A", "is", "A", null, PredicateType.General);
        tracker.PushTopic("B", "is", "B", null, PredicateType.General);
        tracker.PushTopic("C", "is", "C", null, PredicateType.General);

        var recent = tracker.GetRecentTopics(2);
        recent.Count.ShouldBe(2);
        recent[0].Subject.ShouldBe("C");
        recent[1].Subject.ShouldBe("B");
    }

    [Fact]
    public void GetTopicBySubject_ReturnsTopic()
    {
        var tracker = new ContextTracker();
        tracker.PushTopic("Alice", "like", "pizza", "thing", PredicateType.Preference);

        var topic = tracker.GetTopicBySubject("Alice");
        topic.ShouldNotBeNull();
        topic.Object.ShouldBe("pizza");
    }

    [Fact]
    public void GetTopicBySubject_NoMatch_ReturnsNull()
    {
        var tracker = new ContextTracker();
        tracker.PushTopic("Alice", "like", "pizza", "thing", PredicateType.Preference);

        tracker.GetTopicBySubject("Bob").ShouldBeNull();
    }

    [Fact]
    public void Clear_EmptiesTopicStack()
    {
        var tracker = new ContextTracker();
        tracker.PushTopic("Alice", "like", "pizza", "thing", PredicateType.Preference);
        tracker.Clear();
        tracker.TopicStack.Count.ShouldBe(0);
    }

    [Fact]
    public void ResolveFilePronoun_ThatFile_ReturnsCurrentFile()
    {
        var tracker = new ContextTracker();
        tracker.SetContext(ContextKeys.CurrentFile, "Program.cs");
        tracker.ResolveFilePronoun("what does that file do").ShouldBe("Program.cs");
        tracker.ResolveFilePronoun("show me this file").ShouldBe("Program.cs");
    }

    [Fact]
    public void ResolveFilePronoun_ThatTest_ReturnsCurrentFile_WhenEndsInTest()
    {
        var tracker = new ContextTracker();
        tracker.SetContext(ContextKeys.CurrentFile, "ChatSessionTests.cs");
        tracker.ResolveFilePronoun("run that test").ShouldBe("ChatSessionTests.cs");
    }

    [Fact]
    public void ResolveFilePronoun_ThatTest_ReturnsRecentTestFile()
    {
        var tracker = new ContextTracker();
        tracker.SetContext(ContextKeys.RecentFiles, "[\"Program.cs\",\"ChatSessionTests.cs\"]");
        tracker.ResolveFilePronoun("run that test").ShouldBe("ChatSessionTests.cs");
    }

    [Fact]
    public void ResolveFilePronoun_UnknownPronoun_ReturnsNull()
    {
        var tracker = new ContextTracker();
        tracker.ResolveFilePronoun("what do you think").ShouldBeNull();
    }

    [Fact]
    public void ResolveFilePronoun_ThatFunction_ReturnsCurrentFile()
    {
        var tracker = new ContextTracker();
        tracker.SetContext(ContextKeys.CurrentFile, "Program.cs");
        tracker.ResolveFilePronoun("find that function").ShouldBe("Program.cs");
        tracker.ResolveFilePronoun("refactor this method").ShouldBe("Program.cs");
        tracker.ResolveFilePronoun("open that class").ShouldBe("Program.cs");
    }

    [Fact]
    public void ResolveFilePronoun_ThatError_ReturnsNull_WhenNoBuildOutput()
    {
        var tracker = new ContextTracker();
        tracker.ResolveFilePronoun("fix that error").ShouldBeNull();
    }
}
