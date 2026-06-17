using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Contracts;
using EmailService.Consumers;
using EmailService.Data;
using EmailService.Data.Entities;
using Xunit;

namespace UnitTests;

public class EmailConsumerTests
{
    private EmailDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<EmailDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new EmailDbContext(options);
    }

    [Fact]
    public async Task Consume_WithNewMessage_ShouldProcessAndSaveProcessedEvent()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var logger = new Mock<ILogger<EmailConsumer>>();
        var consumer = new EmailConsumer(logger.Object, dbContext);
        
        var messageId = Guid.NewGuid();
        var message = new EmailRequested(
            CorrelationId: Guid.NewGuid(),
            To: "test@example.com",
            Subject: "Test",
            Body: "Body",
            RequestedAt: DateTime.UtcNow
        );

        var context = new Mock<ConsumeContext<EmailRequested>>();
        context.Setup(c => c.Message).Returns(message);
        context.Setup(c => c.MessageId).Returns(messageId);
        context.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        // Act
        await consumer.Consume(context.Object);

        // Assert
        var processedEvent = await dbContext.ProcessedEvents
            .FirstOrDefaultAsync(e => e.EventId == messageId);
        
        processedEvent.Should().NotBeNull();
        processedEvent!.EventType.Should().Be(nameof(EmailRequested));
    }

    [Fact]
    public async Task Consume_WithDuplicateMessage_ShouldSkipProcessing()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var logger = new Mock<ILogger<EmailConsumer>>();
        var consumer = new EmailConsumer(logger.Object, dbContext);
        
        var messageId = Guid.NewGuid();
        var message = new EmailRequested(
            CorrelationId: Guid.NewGuid(),
            To: "test@example.com",
            Subject: "Test",
            Body: "Body",
            RequestedAt: DateTime.UtcNow
        );

        // Добавляем уже обработанное событие
        dbContext.ProcessedEvents.Add(new ProcessedEvent
        {
            EventId = messageId,
            EventType = nameof(EmailRequested),
            ProcessedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var context = new Mock<ConsumeContext<EmailRequested>>();
        context.Setup(c => c.Message).Returns(message);
        context.Setup(c => c.MessageId).Returns(messageId);
        context.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        // Act
        await consumer.Consume(context.Object);

        // Assert
        var count = await dbContext.ProcessedEvents.CountAsync();
        count.Should().Be(1); // Не должно добавиться новое
    }
}