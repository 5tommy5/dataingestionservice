namespace DataIngestionService.Application.DTOs;

public class SourceChannelStat
{
    public string Channel { get; set; } = string.Empty;
    public int TotalTransactions { get; set; }
}

public class StatsSummaryResponse
{
    public int TotalTransactions { get; set; }
    public int TotalCustomers { get; set; }
    public Dictionary<string, decimal> TotalVolumeByCurrency { get; set; } = [];
    // Average number of transactions per day over the last 30 days
    public decimal AvgDailyTransactionCount { get; set; }
    public IReadOnlyList<SourceChannelStat> TopSourceChannels { get; set; } = [];
    public int TransactionsLast24h { get; set; }
}
