using DataIngestionService.Application.DTOs;
using DataIngestionService.Application.Interfaces;
using DataIngestionService.Application.UseCases;
using DataIngestionService.Application.Validators;
using DataIngestionService.Domain.Entities;
using DataIngestionService.Domain.Exceptions;
using FluentValidation;
using Moq;

namespace DataIngestionService.Tests;

public class IngestTransactionUseCaseTests
{
    private readonly Mock<ITransactionRepository> _repositoryMock = new();
    private readonly TransactionRequestValidator _validator = new();

    private IngestTransactionUseCase CreateUseCase() =>
        new(_repositoryMock.Object, _validator);

    [Fact]
    public async Task ExecuteAsync_ValidRequest_InsertsAndReturnsTransaction()
    {
        var request = ValidRequest();
        _repositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<Transaction>()))
            .Returns(Task.CompletedTask);

        var result = await CreateUseCase().ExecuteAsync(request);

        Assert.Equal(request.CustomerId, result.CustomerId);
        Assert.Equal(request.Amount, result.Amount);
        Assert.Equal(request.Currency, result.Currency);
        _repositoryMock.Verify(r => r.InsertAsync(It.IsAny<Transaction>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ThrowsValidationExceptionWithoutCallingRepo()
    {
        var request = ValidRequest();
        request.Amount = -1m;

        await Assert.ThrowsAsync<ValidationException>(() => CreateUseCase().ExecuteAsync(request));
        _repositoryMock.Verify(r => r.InsertAsync(It.IsAny<Transaction>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateKey_ThrowsDuplicateTransactionException()
    {
        var request = ValidRequest();
        _repositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<Transaction>()))
            .ThrowsAsync(new DuplicateTransactionException("some-key"));

        await Assert.ThrowsAsync<DuplicateTransactionException>(() => CreateUseCase().ExecuteAsync(request));
    }

    private static TransactionRequest ValidRequest() => new()
    {
        CustomerId = "cust-1",
        TransactionDate = DateTime.UtcNow.AddHours(-1),
        Amount = 50.00m,
        Currency = "USD",
        SourceChannel = "web"
    };
}
