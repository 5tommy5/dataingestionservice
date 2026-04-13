namespace DataIngestionService.Application.DTOs;

public class CustomerTransactionsResponse
{
    public IReadOnlyList<TransactionItem> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
