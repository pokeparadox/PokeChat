using PokeChat.Enrichment;
using PokeChat.Tools;
using Shouldly;

namespace PokeChat.Tests.Tools;

public class FakeEnricher : IKnowledgeEnricher
{
    private readonly Action<FactRecord>? _onFact;
    private readonly Action<DefinitionRecord>? _onDef;

    public bool IsAvailable => true;

    public FakeEnricher(Action<FactRecord>? onFact = null, Action<DefinitionRecord>? onDef = null)
    {
        _onFact = onFact;
        _onDef = onDef;
    }

    public Task EnrichFactAsync(FactRecord fact, CancellationToken ct = default)
    {
        _onFact?.Invoke(fact);
        return Task.CompletedTask;
    }

    public Task EnrichDefinitionAsync(DefinitionRecord def, CancellationToken ct = default)
    {
        _onDef?.Invoke(def);
        return Task.CompletedTask;
    }
}

public class NullEnricherTests
{
    [Fact]
    public void IsAvailable_ReturnsFalse()
    {
        var enricher = new NullEnricher();
        enricher.IsAvailable.ShouldBeFalse();
    }

    [Fact]
    public void EnrichFactAsync_DoesNotThrow()
    {
        var enricher = new NullEnricher();
        var fact = new FactRecord(1, "Alice", "likes", "pizza", "likes", null, 0, null, null, 1.0, DateTime.UtcNow.ToString("o"));
        Should.NotThrow(() => enricher.EnrichFactAsync(fact));
    }

    [Fact]
    public void EnrichDefinitionAsync_DoesNotThrow()
    {
        var enricher = new NullEnricher();
        var def = new DefinitionRecord("test", "a test word", null, DateTime.UtcNow.ToString("o"));
        Should.NotThrow(() => enricher.EnrichDefinitionAsync(def));
    }
}

public class MemPalaceEnricherTests
{
    private static MemPalaceEnricher CreateEnricher(bool enabled = true)
    {
        var configs = new Dictionary<string, ToolConfig>
        {
            ["mempalace_add_drawer"] = new() { Enabled = enabled }
        };
        var registry = new ToolRegistry(configs);
        return new MemPalaceEnricher(registry, new MemPalaceOptions { Enabled = true, Wing = "test" });
    }

    [Fact]
    public void IsAvailable_WhenToolEnabled_ReturnsTrue()
    {
        var enricher = CreateEnricher();
        enricher.IsAvailable.ShouldBeTrue();
    }

    [Fact]
    public void IsAvailable_WhenToolDisabled_ReturnsFalse()
    {
        var enricher = CreateEnricher(enabled: false);
        enricher.IsAvailable.ShouldBeFalse();
    }

    [Fact]
    public void EnrichFactAsync_WhenUnavailable_DoesNotThrow()
    {
        var enricher = CreateEnricher(enabled: false);
        var fact = new FactRecord(1, "Alice", "likes", "pizza", "likes", null, 0, null, null, 1.0, DateTime.UtcNow.ToString("o"));
        Should.NotThrow(() => enricher.EnrichFactAsync(fact));
    }

    [Fact]
    public void EnrichFactAsync_WhenFactsDisabled_DoesNotThrow()
    {
        var enricher = CreateEnricher();
        var fact = new FactRecord(1, "Alice", "likes", "pizza", "likes", null, 0, null, null, 1.0, DateTime.UtcNow.ToString("o"));
        Should.NotThrow(() => enricher.EnrichFactAsync(fact));
    }

    [Fact]
    public void EnrichDefinitionAsync_WhenUnavailable_DoesNotThrow()
    {
        var enricher = CreateEnricher(enabled: false);
        var def = new DefinitionRecord("test", "a test word", null, DateTime.UtcNow.ToString("o"));
        Should.NotThrow(() => enricher.EnrichDefinitionAsync(def));
    }

    [Fact]
    public void EnrichFactAsync_WithFullData_DoesNotThrow()
    {
        var enricher = CreateEnricher();
        var fact = new FactRecord(1, "Alice", "likes", "pizza", "likes", "positive", 8, "today", DateTime.UtcNow.ToString("o"), 1.0, DateTime.UtcNow.ToString("o"));
        Should.NotThrow(() => enricher.EnrichFactAsync(fact));
    }
}

public class EnrichmentQueueTests
{
    [Fact]
    public void Constructor_StartsProcessor()
    {
        var enricher = new NullEnricher();
        using var queue = new EnrichmentQueue(enricher);
        queue.EnqueuedCount.ShouldBe(0);
    }

    [Fact]
    public void EnqueueFact_WhenUnavailable_DoesNotEnqueue()
    {
        var enricher = new NullEnricher();
        using var queue = new EnrichmentQueue(enricher);
        var fact = new FactRecord(1, "Alice", "likes", "pizza", "likes", null, 0, null, null, 1.0, DateTime.UtcNow.ToString("o"));
        queue.EnqueueFact(fact);
        queue.EnqueuedCount.ShouldBe(0);
    }

    [Fact]
    public void EnqueueFact_WhenAvailable_IncrementsCount()
    {
        var configs = new Dictionary<string, ToolConfig>
        {
            ["mempalace_add_drawer"] = new() { Enabled = true }
        };
        var registry = new ToolRegistry(configs);
        var enricher = new MemPalaceEnricher(registry, new MemPalaceOptions { Enabled = true });
        using var queue = new EnrichmentQueue(enricher);
        var fact = new FactRecord(1, "Alice", "likes", "pizza", "likes", null, 0, null, null, 1.0, DateTime.UtcNow.ToString("o"));
        queue.EnqueueFact(fact);
        queue.EnqueuedCount.ShouldBe(1);
    }

    [Fact]
    public void EnqueueDefinition_WhenUnavailable_DoesNotEnqueue()
    {
        var enricher = new NullEnricher();
        using var queue = new EnrichmentQueue(enricher);
        var def = new DefinitionRecord("test", "a test word", null, DateTime.UtcNow.ToString("o"));
        queue.EnqueueDefinition(def);
        queue.EnqueuedCount.ShouldBe(0);
    }

    [Fact]
    public void EnqueueDefinition_WhenAvailable_IncrementsCount()
    {
        var configs = new Dictionary<string, ToolConfig>
        {
            ["mempalace_add_drawer"] = new() { Enabled = true }
        };
        var registry = new ToolRegistry(configs);
        var enricher = new MemPalaceEnricher(registry, new MemPalaceOptions { Enabled = true });
        using var queue = new EnrichmentQueue(enricher);
        var def = new DefinitionRecord("test", "a test word", null, DateTime.UtcNow.ToString("o"));
        queue.EnqueueDefinition(def);
        queue.EnqueuedCount.ShouldBe(1);
    }

    [Fact]
    public void Dispose_StopsProcessor()
    {
        var enricher = new NullEnricher();
        var queue = new EnrichmentQueue(enricher);
        queue.EnqueueFact(new FactRecord(1, "Alice", "likes", "pizza", "likes", null, 0, null, null, 1.0, DateTime.UtcNow.ToString("o")));
        queue.Dispose();
        Should.NotThrow(() => queue.Dispose());
    }

    [Fact]
    public async Task Queue_ProcessesFact()
    {
        var factCount = 0;
        var fakeEnricher = new FakeEnricher(f => Interlocked.Increment(ref factCount));
        using var queue = new EnrichmentQueue(fakeEnricher);

        var fact = new FactRecord(1, "Alice", "likes", "pizza", "likes", null, 0, null, null, 1.0, DateTime.UtcNow.ToString("o"));
        queue.EnqueueFact(fact);

        await Task.Delay(500);
        queue.ProcessedCount.ShouldBe(1);
        factCount.ShouldBe(1);
    }
}

public class MemPalaceOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var options = new MemPalaceOptions();
        options.Enabled.ShouldBeFalse();
        options.Wing.ShouldBe("pokechat");
        options.EnrichFacts.ShouldBeTrue();
        options.EnrichDefinitions.ShouldBeFalse();
        options.MaxRetries.ShouldBe(3);
        options.RetryDelayMs.ShouldBe(500);
        options.TimeoutMs.ShouldBe(3000);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var options = new MemPalaceOptions
        {
            Enabled = true,
            Wing = "test_wing",
            EnrichFacts = false,
            EnrichDefinitions = true,
            MaxRetries = 5,
            RetryDelayMs = 1000,
            TimeoutMs = 5000
        };

        options.Enabled.ShouldBeTrue();
        options.Wing.ShouldBe("test_wing");
        options.EnrichFacts.ShouldBeFalse();
        options.EnrichDefinitions.ShouldBeTrue();
        options.MaxRetries.ShouldBe(5);
        options.RetryDelayMs.ShouldBe(1000);
        options.TimeoutMs.ShouldBe(5000);
    }
}
