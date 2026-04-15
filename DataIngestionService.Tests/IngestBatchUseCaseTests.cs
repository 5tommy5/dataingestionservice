using System.Text;
using DataIngestionService.Application.Interfaces;
using DataIngestionService.Application.UseCases;
using DataIngestionService.Application.Validators;
using DataIngestionService.Domain.Entities;
using DataIngestionService.Application.Exceptions;
using DataIngestionService.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DataIngestionService.Tests;

public class IngestBatchUseCaseTests
{
    private readonly Mock<ITransactionRepository> _repositoryMock = new();
    private readonly TransactionRequestValidator _validator = new();

    private IngestBatchUseCase CreateUseCase() =>
        new(_repositoryMock.Object, _validator, NullLogger<IngestBatchUseCase>.Instance);

    private static Stream ToCsvStream(string csv) =>
        new MemoryStream(Encoding.UTF8.GetBytes(csv));

    [Fact]
    public async Task ExecuteAsync_AllValidRows_ReturnsFullAcceptedCount()
    {
        const string csv =
            "customer_id,transaction_date,amount,currency,source_channel\n" +
            "cust-1,2024-01-01T00:00:00Z,100.00,USD,web\n" +
            "cust-2,2024-01-02T00:00:00Z,200.00,EUR,mobile\n";

        _repositoryMock
            .Setup(r => r.BulkInsertAsync(It.IsAny<IEnumerable<Transaction>>()))
            .ReturnsAsync((IEnumerable<Transaction> t) => t.Count());

        var result = await CreateUseCase().ExecuteAsync(ToCsvStream(csv));

        Assert.Equal(2, result.Accepted);
        Assert.Equal(0, result.Rejected);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ExecuteAsync_MixedRows_ErrorListContainsCorrectRowNumbers()
    {
        const string csv =
            "customer_id,transaction_date,amount,currency,source_channel\n" +
            "cust-1,2024-01-01T00:00:00Z,100.00,USD,web\n" +
            "cust-2,2024-01-02T00:00:00Z,200.00,INVALID,web\n" +
            "cust-3,2024-01-03T00:00:00Z,75.00,EUR,api\n";

        _repositoryMock
            .Setup(r => r.BulkInsertAsync(It.IsAny<IEnumerable<Transaction>>()))
            .ReturnsAsync((IEnumerable<Transaction> t) => t.Count());

        var result = await CreateUseCase().ExecuteAsync(ToCsvStream(csv));

        Assert.Equal(2, result.Accepted);
        Assert.Equal(1, result.Rejected);
        Assert.Single(result.Errors);
        Assert.Equal("row 2", result.Errors[0].Field);
    }

    [Fact]
    public async Task ExecuteAsync_SemicolonDelimiter_ThrowsInvalidCsvFormatException()
    {
        const string csv =
            "customer_id;transaction_date;amount;currency;source_channel\n" +
            "cust-1;2024-01-01T00:00:00Z;100.00;USD;web\n";

        await Assert.ThrowsAsync<InvalidCsvFormatException>(
            () => CreateUseCase().ExecuteAsync(ToCsvStream(csv)));
    }

    [Fact]
    public async Task ExecuteAsync_HeaderContainsBothDelimiters_ThrowsInvalidCsvFormatException()
    {
        const string csv =
            "customer_id,transaction_date;amount;currency;source_channel\n" +
            "cust-1,2024-01-01T00:00:00Z;100.00;USD;web\n";

        await Assert.ThrowsAsync<InvalidCsvFormatException>(
            () => CreateUseCase().ExecuteAsync(ToCsvStream(csv)));
    }

    [Fact]
    public async Task ExecuteAsync_MalformedLine_TreatedAsRejectedWithValidationError()
    {
        const string csv =
            "customer_id,transaction_date,amount,currency,source_channel\n" +
            "not-enough-columns\n";

        var result = await CreateUseCase().ExecuteAsync(ToCsvStream(csv));

        Assert.Equal(0, result.Accepted);
        Assert.Equal(1, result.Rejected);
        Assert.Single(result.Errors);
        Assert.Equal("row 1", result.Errors[0].Field);
        Assert.Equal("Malformed CSV line", result.Errors[0].Reason);
    }

    [Fact]
    public async Task ExecuteAsync_HeaderOnly_ReturnsZeroAcceptedAndRejected()
    {
        const string csv = "customer_id,transaction_date,amount,currency,source_channel\n";

        var result = await CreateUseCase().ExecuteAsync(ToCsvStream(csv));

        Assert.Equal(0, result.Accepted);
        Assert.Equal(0, result.Rejected);
        Assert.Empty(result.Errors);
        _repositoryMock.Verify(r => r.BulkInsertAsync(It.IsAny<IEnumerable<Transaction>>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateRowsInBatch_ReportsSkippedRowsAsRejected()
    {
        const string csv =
            "customer_id,transaction_date,amount,currency,source_channel\n" +
            "cust-1,2024-01-01T00:00:00Z,100.00,USD,web\n" +
            "cust-1,2024-01-01T00:00:00Z,100.00,USD,web\n" +
            "cust-1,2024-01-01T00:00:00Z,100.00,USD,web\n";

        // Simulate DB ON CONFLICT DO NOTHING: only 1 of the 3 identical rows inserted
        _repositoryMock
            .Setup(r => r.BulkInsertAsync(It.IsAny<IEnumerable<Transaction>>()))
            .ReturnsAsync(1);

        var result = await CreateUseCase().ExecuteAsync(ToCsvStream(csv));

        Assert.Equal(1, result.Accepted);
        Assert.Equal(2, result.Rejected);
        // Duplicate-skipped rows have no validation error entries
        Assert.Empty(result.Errors);
    }
}
