namespace DataIngestionService.Application.DTOs;

public class TransactionRequest
{
    public string CustomerId { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string SourceChannel { get; set; } = string.Empty;
}
