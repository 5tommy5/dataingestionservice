using System.Globalization;
using DataIngestionService.Application.DTOs;
using DataIngestionService.Application.Helpers;
using DataIngestionService.Application.Interfaces;
using DataIngestionService.Domain.Entities;
using DataIngestionService.Domain.Exceptions;
using FluentValidation;

namespace DataIngestionService.Application.UseCases;

public class IngestBatchUseCase
{
    private const int ChunkSize = 5000;

    private readonly ITransactionRepository _repository;
    private readonly IValidator<TransactionRequest> _validator;

    public IngestBatchUseCase(ITransactionRepository repository, IValidator<TransactionRequest> validator)
    {
        _repository = repository;
        _validator = validator;
    }

    public async Task<BatchIngestResponse> ExecuteAsync(Stream csvStream)
    {
        var errors = new List<ValidationError>();
        var chunk = new List<Transaction>(ChunkSize);
        int accepted = 0;
        int rejected = 0;
        int rowNumber = 0;

        using var reader = new StreamReader(csvStream, leaveOpen: true);
        var header = await reader.ReadLineAsync();
        if (header is not null && header.Contains(';') && !header.Contains(','))
            throw new InvalidCsvFormatException("CSV uses ';' as delimiter; expected ','.");

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            rowNumber++;

            var request = ParseCsvLine(line);
            if (request is null)
            {
                errors.Add(new ValidationError($"row {rowNumber}", "Malformed CSV line"));
                rejected++;
                continue;
            }

            var validationResult = await _validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                foreach (var failure in validationResult.Errors)
                    errors.Add(new ValidationError($"row {rowNumber}", failure.ErrorMessage));
                rejected++;
                continue;
            }

            chunk.Add(CreateTransaction(request));

            if (chunk.Count >= ChunkSize)
            {
                var acceptedCount = await _repository.BulkInsertAsync(chunk);
                
                accepted += acceptedCount;
                rejected += chunk.Count - acceptedCount;
                chunk.Clear();
            }
        }

        if (chunk.Count > 0)
        {
            var acceptedCount = await _repository.BulkInsertAsync(chunk);

            accepted += acceptedCount;
            rejected += chunk.Count - acceptedCount;
        }

        return new BatchIngestResponse
        {
            Accepted = accepted,
            Rejected = rejected,
            Errors = errors
        };
    }

    private static TransactionRequest? ParseCsvLine(string line)
    {
        var parts = line.Split(',');
        if (parts.Length < 5)
            return null;

        if (!DateTime.TryParse(parts[1].Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var date))
            return null;

        if (!decimal.TryParse(parts[2].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
            return null;

        return new TransactionRequest
        {
            CustomerId = parts[0].Trim(),
            TransactionDate = date,
            Amount = amount,
            Currency = parts[3].Trim(),
            SourceChannel = parts[4].Trim()
        };
    }

    private static Transaction CreateTransaction(TransactionRequest request) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = request.CustomerId,
        TransactionDate = request.TransactionDate,
        Amount = request.Amount,
        Currency = request.Currency,
        SourceChannel = request.SourceChannel,
        IdempotencyKey = IdempotencyKeyHelper.Compute(request),
        CreatedAt = DateTime.UtcNow
    };
}
