namespace RedHat.Workshop.KafkaAudit.Producer;

/// Valores por defecto del evento. Clase aparte porque C# no admite usar como valor por defecto
/// de un parámetro una constante declarada en el propio tipo.
public static class AuditEventDefaults
{
    public const string EventName = "com.redhat.workshop.kafkaaudit.transfer";
    public const string Body = "Transferencia";
}

/// Evento de auditoría antes de serializarse a OTLP.
///
/// Es un record en vez de 10 parámetros posicionales: Email, Dni y Pan son tres string seguidos, y
/// pasarlos en orden equivocado compilaría sin error y filtraría PII al campo equivocado.
///
/// <param name="EventName">Tipo de evento. Vive aquí y no en el formatter para que la biblioteca
/// sirva a más de un caso de uso, no solo transferencias.</param>
/// <param name="Body">Texto del registro OTLP.</param>
public sealed record AuditEvent(
    string Id,
    string ServiceName,
    string ServiceInstanceId,
    string Environment,
    string Email,
    string Dni,
    string Pan,
    double Amount,
    string Channel,
    int PayloadBytes = 0,
    string EventName = AuditEventDefaults.EventName,
    string Body = AuditEventDefaults.Body);
