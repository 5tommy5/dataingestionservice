using DataIngestionService.Application.DTOs;
using DataIngestionService.Application.Interfaces;
using DataIngestionService.Application.UseCases;
using Moq;

namespace DataIngestionService.Tests;

public class GetStatsSummaryUseCaseTests
{
    private readonly Mock<ITransactionRepository> _repositoryMock = new();
    private readonly Mock<IStatsCache> _cacheMock = new();

    private GetStatsSummaryUseCase CreateUseCase() =>
        new(_repositoryMock.Object, _cacheMock.Object);

    [Fact]
    public async Task ExecuteAsync_CacheHit_ReturnsCachedValueWithoutCallingRepo()
    {
        var cached = new StatsSummaryResponse { TotalTransactions = 42 };
        _cacheMock
            .Setup(c => c.GetAsync("stats:summary"))
            .ReturnsAsync(cached);

        var result = await CreateUseCase().ExecuteAsync();

        Assert.Equal(42, result.TotalTransactions);
        _repositoryMock.Verify(r => r.GetStatsAsync(), Times.Never);
        _cacheMock.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<StatsSummaryResponse>(), It.IsAny<TimeSpan>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_CacheMiss_CallsRepoAndWritesToCache()
    {
        var stats = new StatsSummaryResponse { TotalTransactions = 100 };
        _cacheMock
            .Setup(c => c.GetAsync("stats:summary"))
            .ReturnsAsync((StatsSummaryResponse?)null);
        _repositoryMock
            .Setup(r => r.GetStatsAsync())
            .ReturnsAsync(stats);

        var result = await CreateUseCase().ExecuteAsync();

        Assert.Equal(100, result.TotalTransactions);
        _repositoryMock.Verify(r => r.GetStatsAsync(), Times.Once);
        _cacheMock.Verify(c => c.SetAsync("stats:summary", stats, TimeSpan.FromSeconds(60)), Times.Once);
    }
}
