using Confluent.Kafka;

namespace RedHat.Workshop.KafkaAudit.Producer;

/// Publica en Kafka por mTLS con confirmación de todas las réplicas in-sync.
public sealed class KafkaLogPublisher : ILogPublisher
{
    private readonly IProducer<string, string> _producer;
    private readonly TimeSpan _flushTimeout;

    public KafkaLogPublisher(KafkaTlsOptions options)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = options.Bootstrap,
            SecurityProtocol = SecurityProtocol.Ssl,
            SslCaLocation = options.CaLocation,
            SslCertificateLocation = options.CertificatePath,
            SslKeyLocation = options.PrivateKeyPath,
            // Auditoría bancaria: se confirma solo cuando las réplicas in-sync lo tienen a salvo.
            Acks = Acks.All,
            EnableIdempotence = true,
            // El valor ya va cifrado y por tanto es incompresible: comprimir aquí solo gasta CPU.
            CompressionType = CompressionType.None,
            MessageMaxBytes = options.MaxMessageBytes
        };
        _producer = new ProducerBuilder<string, string>(config).Build();
        _flushTimeout = options.ResolvedFlushTimeout;
        Topic = options.Topic;
    }

    public string Topic { get; }

    public async Task PublishAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var message = new Message<string, string> { Key = key, Value = value };
        // ConfigureAwait(false): código de biblioteca; no capturar el contexto del consumidor.
        await _producer.ProduceAsync(Topic, message, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        // Sin este flush se perderían los mensajes que aún están en el búfer al cerrar el pod.
        _producer.Flush(_flushTimeout);
        _producer.Dispose();
        return ValueTask.CompletedTask;
    }
}
