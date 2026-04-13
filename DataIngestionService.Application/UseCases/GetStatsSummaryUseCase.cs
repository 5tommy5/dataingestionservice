using DataIngestionService.Application.DTOs;
using DataIngestionService.Application.Interfaces;

namespace DataIngestionService.Application.UseCases;

public class GetStatsSummaryUseCase
{
    private const string CacheKey = "stats:summary";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly ITransactionRepository _repository;
    private readonly IStatsCache _cache;

    public GetStatsSummaryUseCase(ITransactionRepository repository, IStatsCache cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public async Task<StatsSummaryResponse> ExecuteAsync()
    {
        var cached = await _cache.GetAsync(CacheKey);
        if (cached is not null)
            return cached;

        var stats = await _repository.GetStatsAsync();
        await _cache.SetAsync(CacheKey, stats, CacheTtl);
        return stats;
    }
}
