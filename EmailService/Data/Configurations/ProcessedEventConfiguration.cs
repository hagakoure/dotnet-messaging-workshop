using EmailService.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shared.Contracts;

namespace EmailService.Data.Configurations;

public class NotificationStatusConfiguration : IEntityTypeConfiguration<ProcessedEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedEvent> builder)
    {
        builder.HasKey(e => e.EventId);
        
        builder.Property(e => e.EventType)
            .IsRequired()
            .HasMaxLength(255); // Ограничение длины для оптимизации хранения
        
    }
}