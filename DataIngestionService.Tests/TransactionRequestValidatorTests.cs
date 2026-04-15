using DataIngestionService.Application.DTOs;
using DataIngestionService.Application.Validators;
using FluentValidation.TestHelper;

namespace DataIngestionService.Tests;

public class TransactionRequestValidatorTests
{
    private readonly TransactionRequestValidator _validator = new();

    [Fact]
    public void Should_HaveError_When_CustomerIdIsEmpty()
    {
        var request = ValidRequest();
        request.CustomerId = "";
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.CustomerId);
    }

    [Fact]
    public void Should_HaveError_When_TransactionDateIsInFuture()
    {
        var request = ValidRequest();
        request.TransactionDate = DateTime.UtcNow.AddDays(1);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.TransactionDate);
    }

    [Fact]
    public void Should_HaveError_When_AmountIsNegative()
    {
        var request = ValidRequest();
        request.Amount = -1m;
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Should_HaveError_When_AmountIsZero()
    {
        var request = ValidRequest();
        request.Amount = 0m;
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Should_HaveError_When_AmountHasMoreThanTwoDecimalPlaces()
    {
        var request = ValidRequest();
        request.Amount = 1.123m;
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Should_HaveError_When_CurrencyIsInvalidIsoCode()
    {
        var request = ValidRequest();
        request.Currency = "ABC";
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Currency);
    }

    [Fact]
    public void Should_HaveError_When_SourceChannelIsEmpty()
    {
        var request = ValidRequest();
        request.SourceChannel = "";
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.SourceChannel);
    }

    [Fact]
    public void Should_NotHaveErrors_When_RequestIsValid()
    {
        var result = _validator.TestValidate(ValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }

    private static TransactionRequest ValidRequest() => new()
    {
        CustomerId = "customer-1",
        TransactionDate = DateTime.UtcNow.AddHours(-1),
        Amount = 100.50m,
        Currency = "USD",
        SourceChannel = "web"
    };
}
