using DataIngestionService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataIngestionService.Infrastructure.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id");

        builder.Property(t => t.CustomerId)
            .HasColumnName("customer_id")
            .IsRequired();

        builder.Property(t => t.TransactionDate)
            .HasColumnName("transaction_date")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(t => t.Amount)
            .HasColumnName("amount")
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.Property(t => t.Currency)
            .HasColumnName("currency")
            .HasColumnType("varchar(3)")
            .IsRequired();

        builder.Property(t => t.SourceChannel)
            .HasColumnName("source_channel")
            .IsRequired();

        builder.Property(t => t.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(t => t.IdempotencyKey)
            .IsUnique();

        builder.HasIndex(t => t.CustomerId);
    }
}
