using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using DataIngestionService.Application.DTOs;
using DataIngestionService.Application.Helpers;
using DataIngestionService.Application.Interfaces;
using DataIngestionService.Domain.Entities;
using DataIngestionService.Application.Exceptions;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace DataIngestionService.Application.UseCases;

public class IngestBatchUseCase
{
    private const int ChunkSize = 15000;

    private readonly ITransactionRepository _repository;
    private readonly IValidator<TransactionRequest> _validator;
    private readonly ILogger<IngestBatchUseCase> _logger;

    public IngestBatchUseCase(ITransactionRepository repository, IValidator<TransactionRequest> validator, ILogger<IngestBatchUseCase> logger)
    {
        _repository = repository;
        _validator = validator;
        _logger = logger;
    }

    public async Task<BatchIngestResponse> ExecuteAsync(Stream csvStream)
    {
        var errors = new List<ValidationError>();
        var chunk = new List<Transaction>(ChunkSize);
        int accepted = 0;
        int rejected = 0;
        int rowNumber = 0;

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            HeaderValidated = null,
            MissingFieldFound = null,
            BadDataFound = null,
        };

        using var reader = new StreamReader(csvStream, leaveOpen: true);
        using var csv = new CsvReader(reader, config);
        csv.Context.RegisterClassMap<TransactionCsvMap>();

        await csv.ReadAsync();
        csv.ReadHeader();

        var headerRecord = csv.HeaderRecord;
        if (headerRecord is null || headerRecord.Length < 5)
            throw new InvalidCsvFormatException("CSV does not contain the required 5 columns. Ensure the file is comma-delimited with headers: customer_id, transaction_date, amount, currency, source_channel.");

        while (await csv.ReadAsync())
        {
            rowNumber++;
            TransactionRequest? request;
            try
            {
                request = csv.GetRecord<TransactionRequest>();
            }
            catch (Exception)
            {
                errors.Add(new ValidationError($"row {rowNumber}", "Malformed CSV line"));
                rejected++;
                continue;
            }

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

        _logger.LogInformation("Batch ingestion complete: {Accepted} accepted, {Rejected} rejected", accepted, rejected);

        return new BatchIngestResponse
        {
            Accepted = accepted,
            Rejected = rejected,
            Errors = errors
        };
    }

    private sealed class TransactionCsvMap : ClassMap<TransactionRequest>
    {
        public TransactionCsvMap()
        {
            Map(m => m.CustomerId).Index(0);
            Map(m => m.TransactionDate).Index(1)
                .TypeConverterOption.DateTimeStyles(DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
            Map(m => m.Amount).Index(2);
            Map(m => m.Currency).Index(3);
            Map(m => m.SourceChannel).Index(4);
        }
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
