using DataIngestionService.Application.DTOs;
using DataIngestionService.Application.UseCases;
using DataIngestionService.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace DataIngestionService.Api.Controllers;

[ApiController]
[Route("ingest")]
public class IngestController : ControllerBase
{
    private readonly IngestTransactionUseCase _ingestTransaction;
    private readonly IngestBatchUseCase _ingestBatch;

    public IngestController(IngestTransactionUseCase ingestTransaction, IngestBatchUseCase ingestBatch)
    {
        _ingestTransaction = ingestTransaction;
        _ingestBatch = ingestBatch;
    }

    [HttpPost("transaction")]
    public async Task<IActionResult> IngestTransaction([FromBody] TransactionRequest request)
    {
        try
        {
            var transaction = await _ingestTransaction.ExecuteAsync(request);
            var item = new TransactionItem
            {
                Id = transaction.Id,
                CustomerId = transaction.CustomerId,
                TransactionDate = transaction.TransactionDate,
                Amount = transaction.Amount,
                Currency = transaction.Currency,
                SourceChannel = transaction.SourceChannel,
                CreatedAt = transaction.CreatedAt
            };
            return CreatedAtAction(nameof(IngestTransaction), new { id = item.Id }, item);
        }
        catch (ValidationException ex)
        {
            var errors = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return ValidationProblem(new ValidationProblemDetails(errors));
        }
        catch (DuplicateTransactionException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Conflict",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
    }

    [HttpPost("batch")]
    public async Task<IActionResult> IngestBatch(IFormFile file)
    {
        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _ingestBatch.ExecuteAsync(stream);
            return Ok(result);
        }
        catch (InvalidCsvFormatException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid CSV format",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }
}
