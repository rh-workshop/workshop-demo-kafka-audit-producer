using System.IO.Compression;
using System.Text;

using RedHat.Workshop.KafkaAudit.Producer;

namespace RedHat.Workshop.KafkaAudit.Producer.Tests;

/// Verifica el orden de la tubería (formatear -> comprimir -> cifrar -> publicar) con dobles de
/// prueba. Las interfaces existían justo para esto y no había un solo test que las usara.
public class AuditLogClientTests
{
    [Fact]
    public async Task Publica_con_el_id_del_evento_como_clave()
    {
        var publisher = new FakePublisher();
        await using var client = new AuditLogClient(new FakeEncryptor(), publisher);

        await client.EmitAsync(Sample());

        Assert.Equal("NET-000001", publisher.LastKey);
    }

    /// El tópico del publisher es lo que se pasa al cifrado como AAD.
    [Fact]
    public async Task Cifra_usando_el_topico_del_publisher_como_aad()
    {
        var encryptor = new FakeEncryptor();
        var publisher = new FakePublisher { Topic = "tp.observability.logs.encrypted" };
        await using var client = new AuditLogClient(encryptor, publisher);

        await client.EmitAsync(Sample());

        Assert.Equal("tp.observability.logs.encrypted", encryptor.LastTopic);
    }

    /// Se comprime ANTES de cifrar: el dato cifrado es incompresible, así que el orden inverso no
    /// ahorraría nada. Lo que llega al cifrado tiene que ser gzip válido.
    [Fact]
    public async Task Comprime_antes_de_cifrar()
    {
        var encryptor = new FakeEncryptor();
        await using var client = new AuditLogClient(encryptor, new FakePublisher());

        await client.EmitAsync(Sample());

        using var input = new MemoryStream(encryptor.LastPlaintext!);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        Assert.NotEmpty(output.ToArray());
    }

    [Fact]
    public async Task Devuelve_los_tamanos_de_cada_etapa()
    {
        await using var client = new AuditLogClient(new FakeEncryptor(), new FakePublisher());

        var result = await client.EmitAsync(Sample());

        Assert.True(result.OtlpBytes > 0);
        Assert.True(result.CompressedBytes > 0);
        Assert.True(result.EncryptedChars > 0);
    }

    /// Una cancelación debe propagarse al publisher y no quedarse a medias.
    [Fact]
    public async Task Propaga_la_cancelacion_al_publisher()
    {
        var publisher = new FakePublisher();
        await using var client = new AuditLogClient(new FakeEncryptor(), publisher);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => client.EmitAsync(Sample(), cancellation.Token));
    }

    private static AuditEvent Sample() => new(
        Id: "NET-000001",
        ServiceName: "bff-canal",
        ServiceInstanceId: "instancia-1",
        Environment: "dev",
        Email: "juan.perez@ejemplo.com",
        Dni: "1712345678",
        Pan: "4539123456789010",
        Amount: 1234.56,
        Channel: "web");

    private sealed class FakeEncryptor : IPayloadEncryptor
    {
        public byte[]? LastPlaintext { get; private set; }

        public string? LastTopic { get; private set; }

        public string Encrypt(byte[] plaintext, string topic)
        {
            LastPlaintext = plaintext;
            LastTopic = topic;
            return Convert.ToBase64String(plaintext);
        }
    }

    private sealed class FakePublisher : ILogPublisher
    {
        public string Topic { get; set; } = "tp.observability.logs.encrypted";

        public string? LastKey { get; private set; }

        public Task PublishAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastKey = key;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
