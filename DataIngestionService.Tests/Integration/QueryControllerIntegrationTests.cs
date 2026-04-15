using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DataIngestionService.Application.DTOs;
using DataIngestionService.Domain.Entities;
using DataIngestionService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace DataIngestionService.Tests.Integration;

[Collection("Integration")]
public class QueryControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public QueryControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Transactions.ExecuteDeleteAsync();
        var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
        await cache.RemoveAsync("stats:summary");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task SeedAsync(IEnumerable<Transaction> transactions)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Transactions.AddRange(transactions);
        await db.SaveChangesAsync();
    }

    private static Transaction MakeTransaction(string customerId, string key, string currency = "USD",
        string sourceChannel = "web", DateTime? date = null, decimal amount = 10m) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = customerId,
        TransactionDate = date ?? new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        Amount = amount,
        Currency = currency,
        SourceChannel = sourceChannel,
        IdempotencyKey = key,
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task GetCustomerTransactions_ReturnsPaginatedResults()
    {
        var customerId = "cust-page";
        var transactions = Enumerable.Range(1, 5).Select(i =>
            MakeTransaction(customerId, $"key-page-{i}", date: new DateTime(2024, 1, i, 0, 0, 0, DateTimeKind.Utc)));
        await SeedAsync(transactions);

        var response = await _client.GetAsync($"/customers/{customerId}/transactions?page=1&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<CustomerTransactionsResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.PageSize);
    }

    [Fact]
    public async Task GetCustomerTransactions_WithDateFilter_ReturnsFilteredResults()
    {
        var customerId = "cust-date";
        await SeedAsync(new[]
        {
            MakeTransaction(customerId, "key-date-jan", date: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), amount: 10m),
            MakeTransaction(customerId, "key-date-mar", date: new DateTime(2024, 3, 15, 0, 0, 0, DateTimeKind.Utc), amount: 20m)
        });

        var response = await _client.GetAsync($"/customers/{customerId}/transactions?dateFrom=2024-02-01T00%3A00%3A00Z&dateTo=2024-12-31T23%3A59%3A59Z");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<CustomerTransactionsResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(20m, result.Items[0].Amount);
    }

    [Fact]
    public async Task GetCustomerTransactions_WithCurrencyFilter_ReturnsFilteredResults()
    {
        var customerId = "cust-currency";
        await SeedAsync(new[]
        {
            MakeTransaction(customerId, "key-cur-usd", currency: "USD"),
            MakeTransaction(customerId, "key-cur-eur", currency: "EUR")
        });

        var response = await _client.GetAsync($"/customers/{customerId}/transactions?currency=EUR");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<CustomerTransactionsResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal("EUR", result.Items[0].Currency);
    }

    [Fact]
    public async Task GetCustomerTransactions_WithSourceChannelFilter_ReturnsFilteredResults()
    {
        var customerId = "cust-channel";
        await SeedAsync(new[]
        {
            MakeTransaction(customerId, "key-ch-web", sourceChannel: "web"),
            MakeTransaction(customerId, "key-ch-mobile", sourceChannel: "mobile")
        });

        var response = await _client.GetAsync($"/customers/{customerId}/transactions?sourceChannel=mobile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<CustomerTransactionsResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal("mobile", result.Items[0].SourceChannel);
    }

    [Fact]
    public async Task GetStatsSummary_Returns200WithCorrectShape()
    {
        await SeedAsync(new[]
        {
            MakeTransaction("cust-a", "key-stats-1", currency: "USD", sourceChannel: "web", amount: 100m,
                date: DateTime.UtcNow.AddHours(-1)),
            MakeTransaction("cust-b", "key-stats-2", currency: "EUR", sourceChannel: "mobile", amount: 200m,
                date: DateTime.UtcNow.AddHours(-2))
        });

        var response = await _client.GetAsync("/stats/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<StatsSummaryResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalTransactions);
        Assert.Equal(2, result.TotalCustomers);
        Assert.Contains("USD", result.TotalVolumeByCurrency.Keys);
        Assert.Contains("EUR", result.TotalVolumeByCurrency.Keys);
        Assert.NotEmpty(result.TopSourceChannels);
        Assert.Equal(2, result.TransactionsLast24h);
    }

    [Fact]
    public async Task GetStatsSummary_SecondCall_ReturnsCachedResult()
    {
        await SeedAsync(new[] { MakeTransaction("cust-cache-a", "key-cache-1", date: DateTime.UtcNow.AddHours(-1)) });

        var first = await _client.GetAsync("/stats/summary");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstResult = await first.Content.ReadFromJsonAsync<StatsSummaryResponse>(JsonOptions);
        Assert.NotNull(firstResult);
        Assert.Equal(1, firstResult.TotalTransactions);

        await SeedAsync(new[] { MakeTransaction("cust-cache-b", "key-cache-2", date: DateTime.UtcNow.AddHours(-1)) });

        var second = await _client.GetAsync("/stats/summary");
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondResult = await second.Content.ReadFromJsonAsync<StatsSummaryResponse>(JsonOptions);
        Assert.NotNull(secondResult);

        Assert.Equal(firstResult.TotalTransactions, secondResult.TotalTransactions);
        Assert.Equal(1, secondResult.TotalTransactions);
    }
}
