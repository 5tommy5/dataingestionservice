using DataIngestionService.Application.DTOs;
using DataIngestionService.Domain.Entities;

namespace DataIngestionService.Application.Interfaces;

public interface ITransactionRepository
{
    Task InsertAsync(Transaction transaction);
    Task<int> BulkInsertAsync(IEnumerable<Transaction> transactions);
    Task<CustomerTransactionsResponse> GetByCustomerIdAsync(string customerId, TransactionQueryParams queryParams);
    Task<StatsSummaryResponse> GetStatsAsync();
}
