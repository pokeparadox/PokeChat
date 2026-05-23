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
}
