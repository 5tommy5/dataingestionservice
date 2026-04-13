using DataIngestionService.Application.DTOs;
using FluentValidation;
using ISO._4217;

namespace DataIngestionService.Application.Validators;

public class TransactionRequestValidator : AbstractValidator<TransactionRequest>
{
    public TransactionRequestValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty();

        RuleFor(x => x.TransactionDate)
            .NotEmpty()
            .LessThanOrEqualTo(_ => DateTime.UtcNow);

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .Must(a => decimal.Round(a, 2) == a)
            .WithMessage("Amount must have at most 2 decimal places.");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Must(c => CurrencyCodesResolver.GetCurrenciesByCode(c).Any())
            .WithMessage(x => $"Invalid currency code: {x.Currency}");

        RuleFor(x => x.SourceChannel)
            .NotEmpty();
    }
}
