using DataIngestionService.Application.DTOs;
using DataIngestionService.Application.Helpers;
using DataIngestionService.Application.Interfaces;
using DataIngestionService.Domain.Entities;
using FluentValidation;

namespace DataIngestionService.Application.UseCases;

public class IngestTransactionUseCase
{
    private readonly ITransactionRepository _repository;
    private readonly IValidator<TransactionRequest> _validator;

    public IngestTransactionUseCase(ITransactionRepository repository, IValidator<TransactionRequest> validator)
    {
        _repository = repository;
        _validator = validator;
    }

    public async Task<Transaction> ExecuteAsync(TransactionRequest request)
    {
        var validationResult = await _validator.ValidateAsync(request);
        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        var transaction = new Transaction
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

        await _repository.InsertAsync(transaction);
        return transaction;
    }
}
