using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DataIngestionService.Application.DTOs;

namespace DataIngestionService.Application.Helpers;

public static class IdempotencyKeyHelper
{
    public static string Compute(TransactionRequest request)
    {
        var raw = $"{request.CustomerId}|{request.TransactionDate:yyyy-MM-dd}|{request.Amount.ToString(CultureInfo.InvariantCulture)}|{request.Currency}|{request.SourceChannel}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
