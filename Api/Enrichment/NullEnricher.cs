namespace PokeChat.Enrichment;

public class NullEnricher : IKnowledgeEnricher
{
    public bool IsAvailable => false;

    public Task EnrichFactAsync(FactRecord fact, CancellationToken ct = default) => Task.CompletedTask;

    public Task EnrichDefinitionAsync(DefinitionRecord def, CancellationToken ct = default) => Task.CompletedTask;
}
