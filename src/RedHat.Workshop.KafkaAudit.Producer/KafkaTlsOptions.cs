namespace RedHat.Workshop.KafkaAudit.Producer;

/// Opciones de conexión mTLS, con los certificados que el operador monta desde el KafkaUser.
///
/// <param name="MaxMessageBytes">Coherente con <c>message.max.bytes</c> del tópico en Strimzi:
/// el sizing estima ~50 KiB por mensaje y el resto es margen para pruebas de carga.</param>
/// <param name="FlushTimeout">Menor que el <c>terminationGracePeriodSeconds</c> del pod, o el
/// flush se corta a media escritura y se pierden los mensajes del búfer.</param>
public sealed record KafkaTlsOptions(
    string Bootstrap,
    string Topic,
    string CertificateDir,
    string CaLocation,
    int MaxMessageBytes = 2_500_000,
    TimeSpan? FlushTimeout = null)
{
    /// Nombres que fija Strimzi en el Secret del KafkaUser; no son elección de esta aplicación.
    private const string UserCertFileName = "user.crt";
    private const string UserKeyFileName = "user.key";

    private static readonly TimeSpan DefaultFlushTimeout = TimeSpan.FromSeconds(5);

    public string CertificatePath => Path.Combine(CertificateDir, UserCertFileName);

    public string PrivateKeyPath => Path.Combine(CertificateDir, UserKeyFileName);

    public TimeSpan ResolvedFlushTimeout => FlushTimeout ?? DefaultFlushTimeout;
}
