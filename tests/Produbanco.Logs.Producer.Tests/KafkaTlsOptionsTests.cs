using Produbanco.Logs.Producer;

namespace Produbanco.Logs.Producer.Tests;

/// Fija los nombres de fichero que monta Strimzi y el timeout de flush por defecto: si alguien
/// los cambia, el pod arranca pero el handshake mTLS falla o el drenado al apagar se corta.
public class KafkaTlsOptionsTests
{
    private static KafkaTlsOptions Options(TimeSpan? flushTimeout = null) => new(
        Bootstrap: "bank-kafka-kafka-bootstrap:9093",
        Topic: "tp.observability.logs.encrypted",
        CertificateDir: "/opt/user",
        CaLocation: "/opt/ca/ca.crt",
        FlushTimeout: flushTimeout);

    /// Los nombres `user.crt`/`user.key` los fija Strimzi en el Secret del KafkaUser; no son
    /// elección de esta aplicación y por eso se congelan aquí.
    [Fact]
    public void Las_rutas_de_certificado_usan_los_nombres_de_strimzi()
    {
        var options = Options();

        Assert.Equal(Path.Combine("/opt/user", "user.crt"), options.CertificatePath);
        Assert.Equal(Path.Combine("/opt/user", "user.key"), options.PrivateKeyPath);
    }

    /// El defecto de 5 s debe quedar por debajo del terminationGracePeriodSeconds del pod, o el
    /// flush se cortaría a media escritura al apagar.
    [Fact]
    public void El_flush_timeout_por_defecto_es_de_cinco_segundos()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), Options().ResolvedFlushTimeout);
    }

    [Fact]
    public void El_flush_timeout_explicito_se_respeta()
    {
        var options = Options(flushTimeout: TimeSpan.FromSeconds(2));

        Assert.Equal(TimeSpan.FromSeconds(2), options.ResolvedFlushTimeout);
    }
}
