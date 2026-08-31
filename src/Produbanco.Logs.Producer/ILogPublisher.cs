namespace Produbanco.Logs.Producer;

/// Permite probar el flujo completo sin un broker Kafka delante.
public interface ILogPublisher : IAsyncDisposable
{
    /// Tópico de destino; el cifrado lo autentica como AAD para atar el ciphertext a su tópico.
    string Topic { get; }

    Task PublishAsync(string key, string value, CancellationToken cancellationToken = default);
}
