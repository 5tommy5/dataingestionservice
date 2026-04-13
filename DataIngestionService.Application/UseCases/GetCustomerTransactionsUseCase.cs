using DataIngestionService.Application.DTOs;
using DataIngestionService.Application.Interfaces;

namespace DataIngestionService.Application.UseCases;

public class GetCustomerTransactionsUseCase
{
    private readonly ITransactionRepository _repository;

    public GetCustomerTransactionsUseCase(ITransactionRepository repository)
    {
        _repository = repository;
    }

    public Task<CustomerTransactionsResponse> ExecuteAsync(string customerId, TransactionQueryParams queryParams)
    {
        return _repository.GetByCustomerIdAsync(customerId, queryParams);
    }
}
