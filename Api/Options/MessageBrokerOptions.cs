namespace Api.Options;

public class MessageBrokerOptions
{
    public const string Section = "MessageBroker";

    /// <summary>
    /// Тип брокера: "rabbitmq" или "kafka"
    /// </summary>
    public string Type { get; init; } = "rabbitmq";
}