using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PokeChat.Tools;

namespace PokeChat.Enrichment;

public class MemPalaceEnricher : IKnowledgeEnricher
{
    private readonly ToolRegistry _toolRegistry;
    private readonly MemPalaceOptions _options;
    private readonly Action<string>? _log;
    private int _consecutiveFailures;
    private const int CircuitBreakerThreshold = 5;

    public bool IsAvailable => _toolRegistry.IsEnabled("mempalace_add_drawer");

    public MemPalaceEnricher(ToolRegistry toolRegistry, MemPalaceOptions options, Action<string>? log = null)
    {
        _toolRegistry = toolRegistry;
        _options = options;
        _log = log;
    }

    public async Task EnrichFactAsync(FactRecord fact, CancellationToken ct = default)
    {
        if (!IsAvailable || !_options.EnrichFacts) return;
        if (_consecutiveFailures >= CircuitBreakerThreshold) return;

        var drawerId = ComputeDeterministicId($"fact:{fact.Subject}:{fact.Verb}:{fact.Object}");
        var content = $"Subject: {fact.Subject}, Verb: {fact.Verb}, Object: {fact.Object}";
        if (fact.Sentiment != null)
            content += $", Sentiment: {fact.Sentiment}";
        if (fact.TimeContext != null)
            content += $", TimeContext: {fact.TimeContext}";

        var payload = JsonSerializer.Serialize(new
        {
            wing = _options.Wing,
            room = "facts",
            drawerId,
            content,
            source = new { project = "pokechat", table = "facts", subject = fact.Subject, verb = fact.Verb, obj = fact.Object }
        });

        await ExecuteWithRetryAsync("mempalace_add_drawer", new[] { payload }, ct);
    }

    public async Task EnrichDefinitionAsync(DefinitionRecord def, CancellationToken ct = default)
    {
        if (!IsAvailable || !_options.EnrichDefinitions) return;
        if (_consecutiveFailures >= CircuitBreakerThreshold) return;

        var drawerId = ComputeDeterministicId($"def:{def.Word}:{def.Definition}");
        var content = $"Word: {def.Word}, Definition: {def.Definition}";

        var payload = JsonSerializer.Serialize(new
        {
            wing = _options.Wing,
            room = "definitions",
            drawerId,
            content,
            source = new { project = "pokechat", table = "word_definitions", word = def.Word }
        });

        await ExecuteWithRetryAsync("mempalace_add_drawer", new[] { payload }, ct);
    }

    private async Task ExecuteWithRetryAsync(string toolName, string[] args, CancellationToken ct)
    {
        for (int attempt = 0; attempt <= _options.MaxRetries; attempt++)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromMilliseconds(_options.TimeoutMs));

                var result = await Task.Run(() => _toolRegistry.TryExecute(toolName, args), cts.Token);
                if (result is { Success: true })
                {
                    Interlocked.Exchange(ref _consecutiveFailures, 0);
                    return;
                }

                _log?.Invoke($"MemPalace enrichment failed (attempt {attempt + 1}): {result?.ErrorMessage ?? "unknown"}");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _log?.Invoke($"MemPalace enrichment error (attempt {attempt + 1}): {ex.Message}");
            }

            if (attempt < _options.MaxRetries)
                await Task.Delay(_options.RetryDelayMs * (attempt + 1), ct).ConfigureAwait(false);
        }

        Interlocked.Increment(ref _consecutiveFailures);
    }

    private static string ComputeDeterministicId(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash[..16]).ToLowerInvariant();
    }
}
