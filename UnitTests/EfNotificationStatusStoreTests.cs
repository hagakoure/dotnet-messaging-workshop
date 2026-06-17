using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;
using Api.Data;
using Api.Data.Entities;
using Api.Services;
using Xunit;

namespace UnitTests;

public class EfNotificationStatusStoreTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateAsync_ShouldSaveStatusAndOutboxMessage()
    {
        // Arrange
        await using var dbContext = CreateInMemoryDbContext();
        var store = new EfNotificationStatusStore(dbContext);
        var correlationId = Guid.NewGuid();
        var request = new EmailRequested(
            CorrelationId: correlationId,
            To: "test@example.com",
            Subject: "Test",
            Body: "Body",
            RequestedAt: DateTime.UtcNow
        );

        // Act
        await store.CreateAsync(correlationId, request, CancellationToken.None);

        // Assert
        var status = await dbContext.NotificationStatuses
            .FirstOrDefaultAsync(s => s.CorrelationId == correlationId);
        
        status.Should().NotBeNull();
        status!.Status.Should().Be(NotificationStatus.Queued);

        var outboxMessage = await dbContext.OutboxMessages
            .FirstOrDefaultAsync(m => m.Payload.Contains(correlationId.ToString()));
        
        outboxMessage.Should().NotBeNull();
        outboxMessage!.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public async Task UpdateStatusAsync_ShouldUpdateExistingStatus()
    {
        // Arrange
        await using var dbContext = CreateInMemoryDbContext();
        var store = new EfNotificationStatusStore(dbContext);
        var correlationId = Guid.NewGuid();
        
        dbContext.NotificationStatuses.Add(new NotificationStatusEntity
        {
            CorrelationId = correlationId,
            Status = NotificationStatus.Queued,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        // Act
        await store.UpdateStatusAsync(correlationId, NotificationStatus.Sent, CancellationToken.None);

        // Assert
        var status = await dbContext.NotificationStatuses
            .FirstOrDefaultAsync(s => s.CorrelationId == correlationId);
        
        status.Should().NotBeNull();
        status!.Status.Should().Be(NotificationStatus.Sent);
    }

    [Fact]
    public async Task GetStatusAsync_WithExistingId_ShouldReturnStatus()
    {
        // Arrange
        await using var dbContext = CreateInMemoryDbContext();
        var store = new EfNotificationStatusStore(dbContext);
        var correlationId = Guid.NewGuid();
        
        dbContext.NotificationStatuses.Add(new NotificationStatusEntity
        {
            CorrelationId = correlationId,
            Status = NotificationStatus.Queued,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        // Act
        var result = await store.GetStatusAsync(correlationId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.CorrelationId.Should().Be(correlationId);
        result.Status.Should().Be(NotificationStatus.Queued);
    }

    [Fact]
    public async Task GetStatusAsync_WithNonExistentId_ShouldReturnNull()
    {
        // Arrange
        await using var dbContext = CreateInMemoryDbContext();
        var store = new EfNotificationStatusStore(dbContext);

        // Act
        var result = await store.GetStatusAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}