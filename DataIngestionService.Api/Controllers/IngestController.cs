using DataIngestionService.Application.DTOs;
using DataIngestionService.Application.UseCases;
using DataIngestionService.Application.Exceptions;
using DataIngestionService.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DataIngestionService.Api.Controllers;

[ApiController]
[Route("ingest")]
public class IngestController : ControllerBase
{
    private const long MaxBatchFileSizeBytes = 100 * 1024 * 1024; // 100 MB

    private readonly IngestTransactionUseCase _ingestTransaction;
    private readonly IngestBatchUseCase _ingestBatch;
    private readonly ILogger<IngestController> _logger;

    public IngestController(IngestTransactionUseCase ingestTransaction, IngestBatchUseCase ingestBatch, ILogger<IngestController> logger)
    {
        _ingestTransaction = ingestTransaction;
        _ingestBatch = ingestBatch;
        _logger = logger;
    }

    [HttpPost("transaction")]
    public async Task<IActionResult> IngestTransaction([FromBody] TransactionRequest request)
    {
        try
        {
            var transaction = await _ingestTransaction.ExecuteAsync(request);
            var item = TransactionItem.FromEntity(transaction);
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
            _logger.LogWarning("Duplicate transaction rejected: {Message}", ex.Message);
            return Conflict(new ProblemDetails
            {
                Title = "Conflict",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
    }

    [HttpPost("batch")]
    [RequestSizeLimit(100 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 100 * 1024 * 1024)]
    public async Task<IActionResult> IngestBatch(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "No file was uploaded.",
                Status = StatusCodes.Status400BadRequest
            });

        var contentType = file.ContentType?.ToLowerInvariant();
        if (contentType is not ("text/csv" or "application/csv" or "application/octet-stream")
            && !file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "File must be a CSV (text/csv).",
                Status = StatusCodes.Status400BadRequest
            });

        if (file.Length > MaxBatchFileSizeBytes)
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = $"File exceeds the maximum allowed size of {MaxBatchFileSizeBytes / 1024 / 1024} MB.",
                Status = StatusCodes.Status400BadRequest
            });

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _ingestBatch.ExecuteAsync(stream, HttpContext.RequestAborted);
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
