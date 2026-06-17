namespace EmailService.Options;

public class KafkaOptions
{
    public const string Section = "Kafka";

    public string BootstrapServers { get; init; } = "localhost:9092";
    public string GroupId { get; init; } = "notification-service";
}