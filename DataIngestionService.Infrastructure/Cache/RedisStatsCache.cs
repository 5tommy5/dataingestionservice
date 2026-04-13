using DataIngestionService.Application.DTOs;
using DataIngestionService.Application.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace DataIngestionService.Infrastructure.Cache;

public class RedisStatsCache : IStatsCache
{
    private readonly IDistributedCache _cache;

    public RedisStatsCache(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<StatsSummaryResponse?> GetAsync(string key)
    {
        var bytes = await _cache.GetAsync(key);
        if (bytes is null) return null;
        return JsonSerializer.Deserialize<StatsSummaryResponse>(bytes);
    }

    public async Task SetAsync(string key, StatsSummaryResponse value, TimeSpan ttl)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl };
        await _cache.SetAsync(key, bytes, options);
    }
}
