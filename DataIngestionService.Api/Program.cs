using DataIngestionService.Application.Interfaces;
using DataIngestionService.Application.UseCases;
using DataIngestionService.Application.Validators;
using DataIngestionService.Infrastructure.Cache;
using DataIngestionService.Infrastructure.Persistence;
using DataIngestionService.Infrastructure.Repositories;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddStackExchangeRedisCache(options =>
    options.Configuration = builder.Configuration.GetConnectionString("Redis"));

builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IStatsCache, RedisStatsCache>();

builder.Services.AddScoped<IngestTransactionUseCase>();
builder.Services.AddScoped<IngestBatchUseCase>();
builder.Services.AddScoped<GetCustomerTransactionsUseCase>();
builder.Services.AddScoped<GetStatsSummaryUseCase>();

builder.Services.AddValidatorsFromAssembly(typeof(TransactionRequestValidator).Assembly);

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.Run();
