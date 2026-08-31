namespace RedHat.Workshop.KafkaAudit.Producer;

/// API pública del paquete: formatea a OTLP, comprime, cifra y publica.
///
/// Recibe sus dependencias por constructor para poder probar el flujo sin Kafka ni llave real.
/// No escribe en consola: es una biblioteca y el registro es responsabilidad de quien la usa.
public sealed class AuditLogClient(IPayloadEncryptor encryptor, ILogPublisher publisher) : IAsyncDisposable
{
    /// Devuelve los tamaños de cada etapa para que el host pueda registrarlos si le interesa.
    public async Task<EmitResult> EmitAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        byte[] otlp = OtlpLogFormatter.Format(auditEvent);
        // Se comprime antes de cifrar: el cifrado deja el dato incompresible.
        byte[] compressed = Compression.Compress(otlp);
        // El tópico entra en el cifrado como AAD, así que el ciphertext solo es válido ahí.
        string encrypted = encryptor.Encrypt(compressed, publisher.Topic);
        // ConfigureAwait(false): biblioteca; no capturar el SynchronizationContext del consumidor.
        await publisher.PublishAsync(auditEvent.Id, encrypted, cancellationToken).ConfigureAwait(false);
        return new EmitResult(otlp.Length, compressed.Length, encrypted.Length);
    }

    public ValueTask DisposeAsync() => publisher.DisposeAsync();
}

/// Tamaños en bytes de cada etapa de la tubería.
public readonly record struct EmitResult(int OtlpBytes, int CompressedBytes, int EncryptedChars);
