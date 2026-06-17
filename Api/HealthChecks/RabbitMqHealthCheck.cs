using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.HealthChecks;

/// <summary>
/// Проверяет доступность RabbitMQ через TCP-сокет.
/// </summary>
public class RabbitMqHealthCheck(string host, int port = 5672) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            // Таймаут подключения 3 секунды
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token);
            
            await client.ConnectAsync(host, port, linkedCts.Token);
            
            return HealthCheckResult.Healthy($"RabbitMQ at {host}:{port} is reachable");
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy($"RabbitMQ connection timeout at {host}:{port}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"RabbitMQ unreachable at {host}:{port}", ex);
        }
    }
}