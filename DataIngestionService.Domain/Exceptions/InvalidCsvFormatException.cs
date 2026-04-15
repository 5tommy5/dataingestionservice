namespace DataIngestionService.Domain.Exceptions;

public class InvalidCsvFormatException : Exception
{
    public InvalidCsvFormatException(string message) : base(message)
    {
    }
}
