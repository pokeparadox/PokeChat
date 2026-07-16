using System.Threading.Channels;

namespace PokeChat.Enrichment;

public class EnrichmentQueue : IDisposable
{
    private readonly Channel<EnrichmentWorkItem> _channel;
    private readonly IKnowledgeEnricher _enricher;
    private readonly Task _processorTask;
    private readonly CancellationTokenSource _cts;
    private readonly Action<string>? _log;
    private int _enqueued;
    private int _processed;
    private int _failed;
    private bool _disposed;

    public int EnqueuedCount => Volatile.Read(ref _enqueued);
    public int ProcessedCount => Volatile.Read(ref _processed);
    public int FailedCount => Volatile.Read(ref _failed);

    public EnrichmentQueue(IKnowledgeEnricher enricher, int capacity = 1000, Action<string>? log = null)
    {
        _enricher = enricher;
        _log = log;
        _channel = Channel.CreateBounded<EnrichmentWorkItem>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });
        _cts = new CancellationTokenSource();
        _processorTask = Task.Run(() => ProcessLoop(_cts.Token));
    }

    public void EnqueueFact(FactRecord fact)
    {
        if (!_enricher.IsAvailable) return;
        Interlocked.Increment(ref _enqueued);
        _channel.Writer.TryWrite(new EnrichmentWorkItem { Fact = fact });
    }

    public void EnqueueDefinition(DefinitionRecord def)
    {
        if (!_enricher.IsAvailable) return;
        Interlocked.Increment(ref _enqueued);
        _channel.Writer.TryWrite(new EnrichmentWorkItem { Definition = def });
    }

    private async Task ProcessLoop(CancellationToken ct)
    {
        await foreach (var item in _channel.Reader.ReadAllAsync(ct))
        {
            try
            {
                if (item.Fact != null)
                    await _enricher.EnrichFactAsync(item.Fact, ct);
                else if (item.Definition != null)
                    await _enricher.EnrichDefinitionAsync(item.Definition, ct);

                Interlocked.Increment(ref _processed);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _failed);
                _log?.Invoke($"Enrichment failed: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _channel.Writer.TryComplete();
        try { _processorTask.Wait(TimeSpan.FromSeconds(2)); }
        catch { /* best effort */ }
        _cts.Dispose();
    }

    private class EnrichmentWorkItem
    {
        public FactRecord? Fact { get; set; }
        public DefinitionRecord? Definition { get; set; }
    }
}
