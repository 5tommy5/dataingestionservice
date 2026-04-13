using DataIngestionService.Application.DTOs;

namespace DataIngestionService.Application.Interfaces;

public interface IStatsCache
{
    Task<StatsSummaryResponse?> GetAsync(string key);
    Task SetAsync(string key, StatsSummaryResponse value, TimeSpan ttl);
}
