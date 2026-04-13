using DataIngestionService.Domain.Entities;
using DataIngestionService.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace DataIngestionService.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TransactionConfiguration());
    }
}
