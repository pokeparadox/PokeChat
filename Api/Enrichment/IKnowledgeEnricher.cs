namespace PokeChat.Enrichment;

public interface IKnowledgeEnricher
{
    Task EnrichFactAsync(FactRecord fact, CancellationToken ct = default);
    Task EnrichDefinitionAsync(DefinitionRecord def, CancellationToken ct = default);
    bool IsAvailable { get; }
}

public record FactRecord(
    int? UserId,
    string Subject,
    string Verb,
    string Object,
    string PredicateType,
    string? Sentiment,
    int EmotionIntensity,
    string? TimeContext,
    string? MentionedAt,
    double Confidence,
    string CreatedAt);

public record DefinitionRecord(
    string Word,
    string Definition,
    int? DefinedByUserId,
    string CreatedAt);
