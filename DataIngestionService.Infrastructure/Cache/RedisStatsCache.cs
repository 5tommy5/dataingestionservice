using DataIngestionService.Application.DTOs;
using DataIngestionService.Application.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DataIngestionService.Infrastructure.Cache;

public class RedisStatsCache : IStatsCache
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisStatsCache> _logger;

    public RedisStatsCache(IDistributedCache cache, ILogger<RedisStatsCache> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<StatsSummaryResponse?> GetAsync(string key)
    {
        try
        {
            var bytes = await _cache.GetAsync(key);
            if (bytes is null) return null;
            return JsonSerializer.Deserialize<StatsSummaryResponse>(bytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache read failed for key {Key}; falling back to database", key);
            return null;
        }
    }

    public async Task SetAsync(string key, StatsSummaryResponse value, TimeSpan ttl)
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
            var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl };
            await _cache.SetAsync(key, bytes, options);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache write failed for key {Key}; stats will not be cached this request", key);
        }
    }
}
