using DataIngestionService.Domain.Entities;

namespace DataIngestionService.Application.DTOs;

public class TransactionItem
{
    public Guid Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string SourceChannel { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public static TransactionItem FromEntity(Transaction t) => new()
    {
        Id = t.Id,
        CustomerId = t.CustomerId,
        TransactionDate = t.TransactionDate,
        Amount = t.Amount,
        Currency = t.Currency,
        SourceChannel = t.SourceChannel,
        CreatedAt = t.CreatedAt
    };
}
