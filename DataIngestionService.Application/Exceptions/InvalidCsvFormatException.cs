namespace DataIngestionService.Application.Exceptions;

public class InvalidCsvFormatException : Exception
{
    public InvalidCsvFormatException(string message) : base(message)
    {
    }
}
