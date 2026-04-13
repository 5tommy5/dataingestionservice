namespace DataIngestionService.Application.DTOs;

public class TransactionQueryParams
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? Currency { get; set; }
    public string? SourceChannel { get; set; }
}
