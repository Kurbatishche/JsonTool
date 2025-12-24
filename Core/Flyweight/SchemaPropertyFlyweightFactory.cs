using System.Collections.Concurrent;

namespace JsonTool.Core.Flyweight;
public class SchemaPropertyFlyweightFactory
{
    private readonly ConcurrentDictionary<string, SchemaPropertyFlyweight> _cache;
    private readonly object _statsLock = new();
    private long _cacheHits;
    private long _cacheMisses;
    private long _totalRequests;
    public int MaxCacheSize { get; }
    public int CacheCount => _cache.Count;
    public long CacheHits => _cacheHits;
    public long CacheMisses => _cacheMisses;
    public long TotalRequests => _totalRequests;
    public double CacheHitRate => _totalRequests > 0 ? (double)_cacheHits / _totalRequests * 100 : 0;
    public SchemaPropertyFlyweightFactory(int maxCacheSize = 0)
    {
        MaxCacheSize = maxCacheSize;
        _cache = new ConcurrentDictionary<string, SchemaPropertyFlyweight>();
        InitializeCommonTypes();
    }
    private void InitializeCommonTypes()
    {
        var commonTypes = new[]
        {
            ("string", (string?)null, (string?)null),
            ("integer", null, null),
            ("number", null, null),
            ("boolean", null, null),
            ("object", null, null),
            ("array", null, null),
            ("null", null, null),
            ("string", "email", null),
            ("string", "uri", null),
            ("string", "date", null),
            ("string", "date-time", null),
            ("string", "time", null),
            ("string", "uuid", null),
            ("string", "hostname", null),
            ("string", "ipv4", null),
            ("string", "ipv6", null),
            ("integer", "int32", null),
            ("integer", "int64", null),
            ("number", "float", null),
            ("number", "double", null)
        };

        foreach (var (type, format, pattern) in commonTypes)
        {
            GetOrCreate(type, format, pattern);
        }
        _cacheHits = 0;
        _cacheMisses = 0;
        _totalRequests = 0;
    }
    public SchemaPropertyFlyweight GetOrCreate(string type, string? format = null, string? pattern = null)
    {
        if (string.IsNullOrEmpty(type))
        {
            throw new ArgumentException("Type cannot be null or empty", nameof(type));
        }

        var key = SchemaPropertyFlyweight.GenerateKey(type, format, pattern);
        
        Interlocked.Increment(ref _totalRequests);

        if (_cache.TryGetValue(key, out var existing))
        {
            Interlocked.Increment(ref _cacheHits);
            existing.IncrementUsage();
            return existing;
        }

        Interlocked.Increment(ref _cacheMisses);
        if (MaxCacheSize > 0 && _cache.Count >= MaxCacheSize)
        {
            EvictLeastUsed();
        }

        var flyweight = new SchemaPropertyFlyweight(type, format, pattern);
        flyweight.IncrementUsage();
        return _cache.GetOrAdd(key, flyweight);
    }
    public SchemaPropertyFlyweight? Get(string key)
    {
        _cache.TryGetValue(key, out var flyweight);
        return flyweight;
    }
    public bool Contains(string type, string? format = null, string? pattern = null)
    {
        var key = SchemaPropertyFlyweight.GenerateKey(type, format, pattern);
        return _cache.ContainsKey(key);
    }
    private void EvictLeastUsed()
    {
        if (_cache.IsEmpty) return;
        var leastUsed = _cache
            .OrderBy(kv => kv.Value.UsageCount)
            .ThenBy(kv => kv.Value.CreatedAt)
            .Take(_cache.Count / 4 + 1) // Видаляємо 25% найменш використовуваних
            .ToList();

        foreach (var item in leastUsed)
        {
            _cache.TryRemove(item.Key, out _);
        }
    }
    public void Clear()
    {
        _cache.Clear();
        lock (_statsLock)
        {
            _cacheHits = 0;
            _cacheMisses = 0;
            _totalRequests = 0;
        }
    }
    public IEnumerable<SchemaPropertyFlyweight> GetAll()
    {
        return _cache.Values.ToList();
    }
    public FlyweightCacheStatistics GetStatistics()
    {
        var flyweights = _cache.Values.ToList();
        
        return new FlyweightCacheStatistics
        {
            CacheSize = _cache.Count,
            TotalRequests = _totalRequests,
            CacheHits = _cacheHits,
            CacheMisses = _cacheMisses,
            HitRate = CacheHitRate,
            TotalUsageCount = flyweights.Sum(f => f.UsageCount),
            AverageUsageCount = flyweights.Count > 0 ? flyweights.Average(f => f.UsageCount) : 0,
            MostUsedFlyweight = flyweights.OrderByDescending(f => f.UsageCount).FirstOrDefault(),
            LeastUsedFlyweight = flyweights.OrderBy(f => f.UsageCount).FirstOrDefault(),
            EstimatedMemorySaved = CalculateMemorySaved(flyweights),
            TypeDistribution = flyweights.GroupBy(f => f.Type).ToDictionary(g => g.Key, g => g.Count())
        };
    }
    private long CalculateMemorySaved(List<SchemaPropertyFlyweight> flyweights)
    {
        long saved = 0;
        foreach (var flyweight in flyweights)
        {
            var timesReused = flyweight.UsageCount - 1;
            if (timesReused > 0)
            {
                saved += timesReused * flyweight.GetApproximateSize();
            }
        }
        return saved;
    }
    public string GetCacheReport()
    {
        var stats = GetStatistics();
        var lines = new List<string>
        {
            "=== Flyweight Cache Report ===",
            $"Cache Size: {stats.CacheSize} flyweights",
            $"Total Requests: {stats.TotalRequests}",
            $"Cache Hits: {stats.CacheHits} ({stats.HitRate:F1}%)",
            $"Cache Misses: {stats.CacheMisses}",
            $"Total Usage Count: {stats.TotalUsageCount}",
            $"Average Usage: {stats.AverageUsageCount:F1}",
            $"Estimated Memory Saved: {FormatBytes(stats.EstimatedMemorySaved)}",
            "",
            "Type Distribution:"
        };

        foreach (var (type, count) in stats.TypeDistribution.OrderByDescending(kv => kv.Value))
        {
            lines.Add($"  {type}: {count}");
        }

        if (stats.MostUsedFlyweight != null)
        {
            lines.Add("");
            lines.Add($"Most Used: {stats.MostUsedFlyweight.GetDisplayType()} ({stats.MostUsedFlyweight.UsageCount}x)");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
public class FlyweightCacheStatistics
{
    public int CacheSize { get; set; }
    public long TotalRequests { get; set; }
    public long CacheHits { get; set; }
    public long CacheMisses { get; set; }
    public double HitRate { get; set; }
    public long TotalUsageCount { get; set; }
    public double AverageUsageCount { get; set; }
    public SchemaPropertyFlyweight? MostUsedFlyweight { get; set; }
    public SchemaPropertyFlyweight? LeastUsedFlyweight { get; set; }
    public long EstimatedMemorySaved { get; set; }
    public Dictionary<string, int> TypeDistribution { get; set; } = new();
}