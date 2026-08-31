namespace Produbanco.Logs.Producer;

/// Nombres de los atributos OTLP del evento de auditoría: el contrato con el processor Java
/// (`AuditAttributes`); los valores deben coincidir literalmente. Se centralizan porque un typo
/// en una clave compila y publica, pero el enmascarado deja de reconocerla y la PII llegaría
/// **sin enmascarar** al tópico `masked` sin que nada falle.
public static class OtlpAttributes
{
    // -- Recurso: identifican al servicio que emite, no al cliente.
    public const string ServiceName = "service.name";
    public const string ServiceNamespace = "service.namespace";
    public const string ServiceVersion = "service.version";
    public const string ServiceInstanceId = "service.instance.id";
    public const string DeploymentEnvironment = "deployment.environment.name";

    // -- Registro.
    public const string EventName = "event.name";
    public const string TransactionAmount = "transaction.amount";
    public const string TransactionChannel = "transaction.channel";
    public const string LogDetail = "log.detail";

    // -- PII: todo atributo listado aquí DEBE tener su regla en el `Masker` del processor.
    public const string CustomerEmail = "customer.email";
    public const string CustomerDni = "customer.dni";
    public const string CardPan = "card.pan";
}
