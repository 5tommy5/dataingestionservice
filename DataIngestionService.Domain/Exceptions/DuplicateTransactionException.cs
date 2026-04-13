namespace DataIngestionService.Domain.Exceptions;

public class DuplicateTransactionException : Exception
{
    public DuplicateTransactionException(string idempotencyKey)
        : base($"A transaction with idempotency key '{idempotencyKey}' already exists.")
    {
    }
}
