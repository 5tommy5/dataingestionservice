using DataIngestionService.Application.DTOs;
using DataIngestionService.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace DataIngestionService.Api.Controllers;

[ApiController]
public class QueryController : ControllerBase
{
    private readonly GetCustomerTransactionsUseCase _getCustomerTransactions;
    private readonly GetStatsSummaryUseCase _getStatsSummary;

    public QueryController(GetCustomerTransactionsUseCase getCustomerTransactions, GetStatsSummaryUseCase getStatsSummary)
    {
        _getCustomerTransactions = getCustomerTransactions;
        _getStatsSummary = getStatsSummary;
    }

    [HttpGet("customers/{id}/transactions")]
    public async Task<IActionResult> GetCustomerTransactions(string id, [FromQuery] TransactionQueryParams queryParams)
    {
        var result = await _getCustomerTransactions.ExecuteAsync(id, queryParams);
        return Ok(result);
    }

    [HttpGet("stats/summary")]
    public async Task<IActionResult> GetStatsSummary()
    {
        var result = await _getStatsSummary.ExecuteAsync();
        return Ok(result);
    }
}
