namespace RedHat.Workshop.KafkaAudit.Host;

using RedHat.Workshop.KafkaAudit.Producer;

/// Configuración del host, leída del entorno en un único sitio.
///
/// Los nombres coinciden con las variables que ya inyecta el Deployment. Los valores por defecto
/// son los de una ejecución local; el Deployment inyecta los suyos (RATE_MS=50 para alcanzar los
/// ~20 msg/s que estima el sizing, PAYLOAD_BYTES=50000 para los ~50 KiB por mensaje).
public sealed record HostOptions(
    string Bootstrap,
    string Topic,
    string CertificateDir,
    string CaFile,
    string KeyFile,
    string Environment,
    int RateMs,
    int PayloadBytes,
    byte KeyId,
    string KeyInfo)
{
    public static HostOptions FromEnvironment() => new(
        Read("BOOTSTRAP", "bank-kafka-kafka-bootstrap:9093"),
        Read("TOPIC", "tp.observability.logs.encrypted"),
        Read("CERT_DIR", "/opt/user"),
        Read("CA_FILE", "/opt/ca/ca.crt"),
        Read("KV_KEY_FILE", "/opt/kv/aes-key"),
        Read("ENVIRONMENT", "dev"),
        ReadInt("RATE_MS", 10_000),
        ReadInt("PAYLOAD_BYTES", 0),
        // Identifica la llave dentro del payload, para poder rotarla sin perder lo ya publicado.
        (byte)ReadInt("KEY_ID", 1),
        Read("KEY_INFO", KeyDerivation.InfoV2));

    private static string Read(string name, string fallback)
    {
        string? value = System.Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    /// Un valor mal escrito (RATE_MS=5O) aborta en vez de caer al fallback en silencio: en una
    /// prueba de carga, descubrir que la tasa real era la de por defecto cuesta horas.
    private static int ReadInt(string name, int fallback)
    {
        string? raw = System.Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }
        if (!int.TryParse(raw, out int value) || value < 0)
        {
            throw new InvalidOperationException(
                $"la variable {name} debe ser un entero no negativo, y llegó '{raw}'");
        }
        return value;
    }
}
