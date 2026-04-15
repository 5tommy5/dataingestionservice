using DataIngestionService.Application.DTOs;
using DataIngestionService.Application.Interfaces;
using DataIngestionService.Domain.Entities;
using DataIngestionService.Domain.Exceptions;
using DataIngestionService.Infrastructure.Persistence;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DataIngestionService.Infrastructure.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly AppDbContext _context;

    public TransactionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task InsertAsync(Transaction transaction)
    {
        try
        {
            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DuplicateTransactionException(transaction.IdempotencyKey);
        }
    }

    public async Task<int> BulkInsertAsync(IEnumerable<Transaction> transactions)
    {
        var bulkConfig = new BulkConfig
        {
            UpdateByProperties = [nameof(Transaction.IdempotencyKey)],
            PropertiesToIncludeOnUpdate = new List<string> { "" },
            CalculateStats = true
        };
        try
        {
            await _context.BulkInsertOrUpdateAsync(transactions.ToList(), bulkConfig);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Bulk insert failed. The batch could not be persisted.", ex);
        }

        return bulkConfig.StatsInfo?.StatsNumberInserted ?? 0;
    }

    public async Task<CustomerTransactionsResponse> GetByCustomerIdAsync(string customerId, TransactionQueryParams queryParams)
    {
        var query = _context.Transactions.Where(t => t.CustomerId == customerId);

        if (queryParams.DateFrom.HasValue)
            query = query.Where(t => t.TransactionDate >= queryParams.DateFrom.Value);

        if (queryParams.DateTo.HasValue)
            query = query.Where(t => t.TransactionDate <= queryParams.DateTo.Value);

        if (!string.IsNullOrEmpty(queryParams.Currency))
            query = query.Where(t => t.Currency == queryParams.Currency);

        if (!string.IsNullOrEmpty(queryParams.SourceChannel))
            query = query.Where(t => t.SourceChannel == queryParams.SourceChannel);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(t => t.TransactionDate)
            .Skip((queryParams.Page - 1) * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .Select(t => new TransactionItem
            {
                Id = t.Id,
                CustomerId = t.CustomerId,
                TransactionDate = t.TransactionDate,
                Amount = t.Amount,
                Currency = t.Currency,
                SourceChannel = t.SourceChannel,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync();

        return new CustomerTransactionsResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = queryParams.Page,
            PageSize = queryParams.PageSize
        };
    }

    public async Task<StatsSummaryResponse> GetStatsAsync()
    {
        var now = DateTime.UtcNow;
        var cutoff30Days = now.AddDays(-30);
        var cutoff24h = now.AddHours(-24);

        var totalTransactions = await _context.Transactions.CountAsync();

        var totalCustomers = await _context.Transactions
            .Select(t => t.CustomerId)
            .Distinct()
            .CountAsync();

        var volumeByCurrency = await _context.Transactions
            .GroupBy(t => t.Currency)
            .Select(g => new { Currency = g.Key, Total = g.Sum(t => t.Amount) })
            .ToListAsync();

        var dailyCounts = await _context.Transactions
            .Where(t => t.TransactionDate >= cutoff30Days)
            .GroupBy(t => t.TransactionDate.Date)
            .Select(g => (decimal)g.Count())
            .ToListAsync();

        var avgDailyTransactionCount = dailyCounts.Count > 0 ? Math.Round(dailyCounts.Sum() / 30m, 2) : 0m;

        var topChannels = await _context.Transactions
            .GroupBy(t => t.SourceChannel)
            .Select(g => new SourceChannelStat { Channel = g.Key, TotalTransactions = g.Count() })
            .OrderByDescending(x => x.TotalTransactions)
            .Take(5)
            .ToListAsync();

        var transactionsLast24h = await _context.Transactions
            .CountAsync(t => t.TransactionDate >= cutoff24h);

        return new StatsSummaryResponse
        {
            TotalTransactions = totalTransactions,
            TotalCustomers = totalCustomers,
            TotalVolumeByCurrency = volumeByCurrency.ToDictionary(x => x.Currency, x => x.Total),
            AvgDailyTransactionCount = avgDailyTransactionCount,
            TopSourceChannels = topChannels,
            TransactionsLast24h = transactionsLast24h
        };
    }
}
