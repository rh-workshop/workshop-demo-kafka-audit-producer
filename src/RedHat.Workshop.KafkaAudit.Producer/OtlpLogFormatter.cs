using System.Security.Cryptography;

using Google.Protobuf;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Resource.V1;

namespace RedHat.Workshop.KafkaAudit.Producer;

/// Serializa un evento de auditoría a OTLP Protobuf (clases oficiales de OpenTelemetry). El esquema
/// debe coincidir con el del pipeline Java: ambos escriben en el mismo tópico. Los nombres de
/// atributo salen de <see cref="OtlpAttributes"/>, el contrato compartido con el enmascarado.
public static class OtlpLogFormatter
{
    private const string SchemaUrl = "https://opentelemetry.io/schemas/1.36.0";
    private const string ScopeName = "RedHat.Workshop.KafkaAudit.Producer";
    private const string ServiceVersion = "1.0.0";
    private const string ServiceNamespace = "kafka-audit";
    private const string SeverityInfoText = "INFO";

    /// Longitud del span id según el spec de OTLP.
    private const int SpanIdLength = 8;

    /// Bit `sampled` del W3C Trace Context.
    private const uint TraceFlagSampled = 0x01;

    private const long NanosPerMillisecond = 1_000_000L;

    public static byte[] Format(AuditEvent auditEvent)
    {
        var data = new LogsData
        {
            ResourceLogs =
            {
                new ResourceLogs
                {
                    SchemaUrl = SchemaUrl,
                    Resource = BuildResource(auditEvent),
                    ScopeLogs =
                    {
                        new ScopeLogs
                        {
                            SchemaUrl = SchemaUrl,
                            Scope = new InstrumentationScope { Name = ScopeName, Version = ServiceVersion },
                            LogRecords = { BuildRecord(auditEvent) },
                        },
                    },
                },
            },
        };
        return data.ToByteArray();
    }

    private static Resource BuildResource(AuditEvent auditEvent) =>
        new()
        {
            Attributes =
            {
                Text(OtlpAttributes.ServiceName, auditEvent.ServiceName),
                Text(OtlpAttributes.ServiceNamespace, ServiceNamespace),
                Text(OtlpAttributes.ServiceVersion, ServiceVersion),
                Text(OtlpAttributes.ServiceInstanceId, auditEvent.ServiceInstanceId),
                Text(OtlpAttributes.DeploymentEnvironment, auditEvent.Environment),
            },
        };

    private static LogRecord BuildRecord(AuditEvent auditEvent)
    {
        ulong nowNanos = (ulong)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * NanosPerMillisecond);
        byte[] traceId = NewTraceId();

        var record = new LogRecord
        {
            TimeUnixNano = nowNanos,
            ObservedTimeUnixNano = nowNanos,
            SeverityNumber = SeverityNumber.Info,
            SeverityText = SeverityInfoText,
            Body = new AnyValue { StringValue = auditEvent.Body },
            TraceId = ByteString.CopyFrom(traceId),
            // El span id son los 8 primeros bytes del trace: basta para correlacionar en la demo.
            SpanId = ByteString.CopyFrom(traceId[..SpanIdLength]),
            Flags = TraceFlagSampled,
            Attributes =
            {
                // La v1.3.2 del proto aún no tiene event_name como campo, así que va como atributo.
                Text(OtlpAttributes.EventName, auditEvent.EventName),
                Text(OtlpAttributes.CustomerEmail, auditEvent.Email),
                Text(OtlpAttributes.CustomerDni, auditEvent.Dni),
                Text(OtlpAttributes.CardPan, auditEvent.Pan),
                new KeyValue
                {
                    Key = OtlpAttributes.TransactionAmount,
                    Value = new AnyValue { DoubleValue = auditEvent.Amount },
                },
                Text(OtlpAttributes.TransactionChannel, auditEvent.Channel),
            },
        };

        // Relleno para alcanzar el tamaño de mensaje del sizing; aleatorio para que no comprima.
        if (auditEvent.PayloadBytes > 0)
        {
            record.Attributes.Add(Text(OtlpAttributes.LogDetail, RandomHex(auditEvent.PayloadBytes)));
        }

        return record;
    }

    /// Big-endian, como exige W3C Trace Context: el ToByteArray() por defecto de Guid usa un
    /// orden mixto propio de .NET y los identificadores no correlacionarían en el backend.
    private static byte[] NewTraceId() => Guid.NewGuid().ToByteArray(bigEndian: true);

    private static string RandomHex(int length) =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes((length + 1) / 2))[..length];

    private static KeyValue Text(string key, string value) =>
        new() { Key = key, Value = new AnyValue { StringValue = value } };
}
