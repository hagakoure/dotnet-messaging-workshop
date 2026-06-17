using EmailService.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmailService.Data;

public class EmailDbContext(DbContextOptions<EmailDbContext> options) : DbContext(options)
{
    public DbSet<ProcessedEvent> ProcessedEvents { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Автоматически применяет все классы IEntityTypeConfiguration из текущей сборки
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EmailDbContext).Assembly);
        
        base.OnModelCreating(modelBuilder);
    }
}

