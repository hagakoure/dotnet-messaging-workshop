namespace Api.Options;

public class RabbitMqOptions
{
    public const string Section = "RabbitMQ";
    
    public string Host { get; init; } = "localhost";
    public string UserName { get; init; } = "guest";
    public string Password { get; init; } = "guest";
}