using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shared.Contracts;

namespace Api.Data.Configurations;

public class NotificationStatusConfiguration : IEntityTypeConfiguration<NotificationStatusEntity>
{
    public void Configure(EntityTypeBuilder<NotificationStatusEntity> builder)
    {
        builder.HasKey(e => e.CorrelationId);

        // Конвертация enum в string для хранения в БД
        builder.Property(e => e.Status)
            .HasConversion(
                status => status.ToString(),
                value => (NotificationStatus)Enum.Parse(typeof(NotificationStatus), value)
            );
    }
}