using System.Runtime.InteropServices;

using Confluent.Kafka;

using Produbanco.Logs.Host;
using Produbanco.Logs.Producer;

// Host de demostración de la biblioteca: emite eventos ficticios hasta que se le pide parar.
var options = HostOptions.FromEnvironment();
// El trim es imprescindible: un salto de línea final daría una llave distinta a la del Java.
string secret = File.ReadAllText(options.KeyFile).Trim();

using var cancellation = new CancellationTokenSource();

// SIGTERM es lo que envía Kubernetes al terminar el pod: sin atenderlo, el flush del productor
// no llega a ejecutarse y se pierden los mensajes que quedan en el búfer.
using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
{
    context.Cancel = true;
    cancellation.Cancel();
});
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

using var encryptor = new PayloadEncryptor(secret, options.KeyId, options.KeyInfo);
var tls = new KafkaTlsOptions(options.Bootstrap, options.Topic, options.CertificateDir, options.CaFile);
await using var client = new AuditLogClient(encryptor, new KafkaLogPublisher(tls));

var events = new FakeAuditEvents(options.Environment, options.PayloadBytes);
Console.WriteLine($"productor .NET listo: bootstrap={options.Bootstrap} topic={options.Topic} " +
                  $"rateMs={options.RateMs} payloadBytes={options.PayloadBytes}");

while (!cancellation.IsCancellationRequested)
{
    var auditEvent = events.Next();
    try
    {
        var result = await client.EmitAsync(auditEvent, cancellation.Token);
        Console.WriteLine($"{auditEvent.Id} OTLP {result.OtlpBytes} B -> comprimido " +
                          $"{result.CompressedBytes} B -> cifrado {result.EncryptedChars} chars");
    }
    catch (OperationCanceledException)
    {
        break;
    }
    catch (ProduceException<string, string> e) when (!e.Error.IsFatal)
    {
        // Fallo transitorio del broker: se registra con la excepción completa y se reintenta.
        Console.Error.WriteLine($"{auditEvent.Id} no publicado: {e}");
    }
    // Un error fatal (TLS mal configurado, ACL denegada) no se captura a propósito: el proceso
    // termina y el CrashLoop lo hace visible, en vez de quedar reintentando en silencio.

    if (options.RateMs > 0)
    {
        try
        {
            await Task.Delay(options.RateMs, cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            break;
        }
    }
}

Console.WriteLine("productor .NET detenido");
