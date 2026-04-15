using DataIngestionService.Application.DTOs;
using DataIngestionService.Application.Helpers;

namespace DataIngestionService.Tests;

public class IdempotencyKeyHelperTests
{
    private static TransactionRequest BaseRequest() => new()
    {
        CustomerId = "cust-1",
        TransactionDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        Amount = 100.00m,
        Currency = "USD",
        SourceChannel = "web"
    };

    [Fact]
    public void Compute_SameInput_ReturnsSameHash()
    {
        var request = BaseRequest();

        var hash1 = IdempotencyKeyHelper.Compute(request);
        var hash2 = IdempotencyKeyHelper.Compute(request);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Compute_ReturnsLowercaseHexString64Chars()
    {
        var hash = IdempotencyKeyHelper.Compute(BaseRequest());

        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void Compute_DifferentCustomerId_ReturnsDifferentHash()
    {
        var r1 = BaseRequest();
        var r2 = BaseRequest();
        r2.CustomerId = "cust-2";

        Assert.NotEqual(IdempotencyKeyHelper.Compute(r1), IdempotencyKeyHelper.Compute(r2));
    }

    [Fact]
    public void Compute_DifferentTransactionDate_ReturnsDifferentHash()
    {
        var r1 = BaseRequest();
        var r2 = BaseRequest();
        r2.TransactionDate = new DateTime(2024, 6, 15, 12, 30, 0, DateTimeKind.Utc);

        Assert.NotEqual(IdempotencyKeyHelper.Compute(r1), IdempotencyKeyHelper.Compute(r2));
    }

    [Fact]
    public void Compute_DifferentAmount_ReturnsDifferentHash()
    {
        var r1 = BaseRequest();
        var r2 = BaseRequest();
        r2.Amount = 200.00m;

        Assert.NotEqual(IdempotencyKeyHelper.Compute(r1), IdempotencyKeyHelper.Compute(r2));
    }

    [Fact]
    public void Compute_DifferentCurrency_ReturnsDifferentHash()
    {
        var r1 = BaseRequest();
        var r2 = BaseRequest();
        r2.Currency = "EUR";

        Assert.NotEqual(IdempotencyKeyHelper.Compute(r1), IdempotencyKeyHelper.Compute(r2));
    }

    [Fact]
    public void Compute_DifferentSourceChannel_ReturnsDifferentHash()
    {
        var r1 = BaseRequest();
        var r2 = BaseRequest();
        r2.SourceChannel = "mobile";

        Assert.NotEqual(IdempotencyKeyHelper.Compute(r1), IdempotencyKeyHelper.Compute(r2));
    }
}
