using System.Collections.Concurrent;

namespace PokeChat.Api.Services;

public class TokenBucketOptions
{
    public bool Enabled { get; set; } = true;
    public int TokensPerMinute { get; set; } = 20;
    public int MaxTokens { get; set; } = 20;
    public int NlpCost { get; set; } = 1;
    public int UpstreamCost { get; set; } = 20;
    public int StreamNlpCost { get; set; } = 2;
    public int StreamUpstreamCost { get; set; } = 25;
}

public class TokenBucket
{
    public double Tokens { get; set; }
    public DateTime LastRefilled { get; set; } = DateTime.UtcNow;
}

public interface ITokenBucketStore
{
    bool TryDeduct(string key, int cost);
    int GetRemaining(string key);
    int GetResetSeconds(string key);
}

public class InMemoryTokenBucketStore : ITokenBucketStore
{
    private readonly ConcurrentDictionary<string, TokenBucket> _buckets = new(StringComparer.OrdinalIgnoreCase);
    private readonly TokenBucketOptions _options;

    public InMemoryTokenBucketStore(TokenBucketOptions options)
    {
        _options = options;
    }

    public bool TryDeduct(string key, int cost)
    {
        if (!_options.Enabled) return true;

        var now = DateTime.UtcNow;
        var bucket = _buckets.GetOrAdd(key, _ => new TokenBucket { Tokens = _options.MaxTokens, LastRefilled = now });

        lock (bucket)
        {
            Refill(bucket, now);

            if (bucket.Tokens < cost)
                return false;

            bucket.Tokens -= cost;
            return true;
        }
    }

    public int GetRemaining(string key)
    {
        var now = DateTime.UtcNow;
        if (!_buckets.TryGetValue(key, out var bucket))
            return _options.MaxTokens;

        lock (bucket)
        {
            Refill(bucket, now);
            return (int)bucket.Tokens;
        }
    }

    public int GetResetSeconds(string key)
    {
        if (!_buckets.TryGetValue(key, out var bucket))
            return 60;

        lock (bucket)
        {
            var elapsed = (DateTime.UtcNow - bucket.LastRefilled).TotalSeconds;
            var remaining = 60 - elapsed;
            return remaining < 0 ? 0 : (int)remaining;
        }
    }

    private void Refill(TokenBucket bucket, DateTime now)
    {
        var elapsed = (now - bucket.LastRefilled).TotalSeconds;
        if (elapsed < 1) return;

        var refillAmount = elapsed * (_options.TokensPerMinute / 60.0);
        bucket.Tokens = System.Math.Min(_options.MaxTokens, bucket.Tokens + refillAmount);
        bucket.LastRefilled = now;
    }
}
