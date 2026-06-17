namespace Api.Services;

public interface IMessagePublisher
{
    Task PublishAsync(object message, CancellationToken cancellationToken);
}