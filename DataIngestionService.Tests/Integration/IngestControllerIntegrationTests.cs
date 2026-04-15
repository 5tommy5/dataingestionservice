using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DataIngestionService.Application.DTOs;
using DataIngestionService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DataIngestionService.Tests.Integration;

[Collection("Integration")]
public class IngestControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public IngestControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Transactions.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PostTransaction_ValidRequest_Returns201WithTransactionBody()
    {
        var request = new
        {
            customerId = "cust-1",
            transactionDate = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc),
            amount = 50.00m,
            currency = "USD",
            sourceChannel = "web"
        };

        var response = await _client.PostAsJsonAsync("/ingest/transaction", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var item = await response.Content.ReadFromJsonAsync<TransactionItem>(JsonOptions);
        Assert.NotNull(item);
        Assert.Equal("cust-1", item.CustomerId);
        Assert.Equal(50.00m, item.Amount);
        Assert.Equal("USD", item.Currency);
        Assert.Equal("web", item.SourceChannel);
        Assert.NotEqual(Guid.Empty, item.Id);
    }

    [Fact]
    public async Task PostTransaction_InvalidRequest_Returns400()
    {
        var request = new
        {
            customerId = "",
            transactionDate = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc),
            amount = -1m,
            currency = "USD",
            sourceChannel = "web"
        };

        var response = await _client.PostAsJsonAsync("/ingest/transaction", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostTransaction_DuplicateTransaction_Returns409()
    {
        var request = new
        {
            customerId = "cust-dup",
            transactionDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            amount = 100.00m,
            currency = "USD",
            sourceChannel = "web"
        };

        var first = await _client.PostAsJsonAsync("/ingest/transaction", request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await _client.PostAsJsonAsync("/ingest/transaction", request);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task PostBatch_ValidCsv_Returns200WithCorrectAcceptedCount()
    {
        const string csv =
            "customer_id,transaction_date,amount,currency,source_channel\n" +
            "cust-1,2024-01-01T00:00:00Z,100.00,USD,web\n" +
            "cust-2,2024-01-02T00:00:00Z,200.00,EUR,mobile\n";

        using var content = BuildCsvContent(csv);

        var response = await _client.PostAsync("/ingest/batch", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<BatchIngestResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(2, result.Accepted);
        Assert.Equal(0, result.Rejected);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task PostBatch_MixedCsv_ReturnsCorrectAcceptedAndRejectedCounts()
    {
        const string csv =
            "customer_id,transaction_date,amount,currency,source_channel\n" +
            "cust-1,2024-01-01T00:00:00Z,100.00,USD,web\n" +
            "cust-2,2024-01-02T00:00:00Z,200.00,INVALID,web\n" +
            "cust-3,2024-01-03T00:00:00Z,75.00,EUR,api\n";

        using var content = BuildCsvContent(csv);

        var response = await _client.PostAsync("/ingest/batch", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<BatchIngestResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(2, result.Accepted);
        Assert.Equal(1, result.Rejected);
        Assert.Single(result.Errors);
    }

    private static MultipartFormDataContent BuildCsvContent(string csv)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(csv, Encoding.UTF8, "text/csv"), "file", "transactions.csv");
        return content;
    }
}
