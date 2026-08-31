using Google.Protobuf;
using OpenTelemetry.Proto.Logs.V1;

using RedHat.Workshop.KafkaAudit.Producer;

namespace RedHat.Workshop.KafkaAudit.Producer.Tests;

/// Verifica el mensaje OTLP que produce la biblioteca. El valor de estos tests es que convierten
/// un typo en un nombre de atributo —que hoy compilaría y dejaría la PII sin enmascarar— en un
/// fallo detectable en CI.
public class OtlpLogFormatterTests
{
    [Fact]
    public void Publica_los_atributos_de_pii_con_los_nombres_que_espera_el_masker()
    {
        var record = FirstRecord(Sample());

        Assert.Equal("juan.perez@ejemplo.com", Text(record, OtlpAttributes.CustomerEmail));
        Assert.Equal("1712345678", Text(record, OtlpAttributes.CustomerDni));
        Assert.Equal("4539123456789010", Text(record, OtlpAttributes.CardPan));
    }

    [Fact]
    public void Publica_los_atributos_del_recurso_con_los_nombres_acordados()
    {
        var resource = LogsData.Parser.ParseFrom(OtlpLogFormatter.Format(Sample()))
            .ResourceLogs[0].Resource;

        Assert.Contains(resource.Attributes, a => a.Key == OtlpAttributes.ServiceName);
        Assert.Contains(resource.Attributes, a => a.Key == OtlpAttributes.ServiceNamespace);
        Assert.Contains(resource.Attributes, a => a.Key == OtlpAttributes.ServiceVersion);
        Assert.Contains(resource.Attributes, a => a.Key == OtlpAttributes.ServiceInstanceId);
        Assert.Contains(resource.Attributes, a => a.Key == OtlpAttributes.DeploymentEnvironment);
    }

    /// El importe va como double, no como texto: si fuera texto el masker lo trataría como
    /// candidato a enmascarar.
    [Fact]
    public void El_importe_viaja_como_numero()
    {
        var record = FirstRecord(Sample());

        var amount = record.Attributes.Single(a => a.Key == OtlpAttributes.TransactionAmount);
        Assert.Equal(1234.56, amount.Value.DoubleValue);
    }

    /// W3C Trace Context: 16 bytes de trace y 8 de span, y el span es el prefijo del trace.
    [Fact]
    public void El_trace_id_y_el_span_id_tienen_la_longitud_del_spec()
    {
        var record = FirstRecord(Sample());

        Assert.Equal(16, record.TraceId.Length);
        Assert.Equal(8, record.SpanId.Length);
        Assert.Equal(record.TraceId.ToByteArray()[..8], record.SpanId.ToByteArray());
    }

    [Fact]
    public void Sin_relleno_no_se_emite_el_atributo_de_detalle()
    {
        var record = FirstRecord(Sample());

        Assert.DoesNotContain(record.Attributes, a => a.Key == OtlpAttributes.LogDetail);
    }

    [Fact]
    public void El_relleno_alcanza_la_longitud_pedida()
    {
        var record = FirstRecord(Sample() with { PayloadBytes = 501 });

        var detail = record.Attributes.Single(a => a.Key == OtlpAttributes.LogDetail);
        Assert.Equal(501, detail.Value.StringValue.Length);
    }

    /// El tipo de evento sale del propio evento: la biblioteca ya no está atada a "transferencia".
    [Fact]
    public void El_tipo_de_evento_se_puede_personalizar()
    {
        var record = FirstRecord(Sample() with { EventName = "com.redhat.workshop.kafkaaudit.login" });

        Assert.Equal("com.redhat.workshop.kafkaaudit.login", Text(record, OtlpAttributes.EventName));
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

    private static LogRecord FirstRecord(AuditEvent auditEvent) =>
        LogsData.Parser.ParseFrom(OtlpLogFormatter.Format(auditEvent))
            .ResourceLogs[0].ScopeLogs[0].LogRecords[0];

    private static string Text(LogRecord record, string key) =>
        record.Attributes.Single(a => a.Key == key).Value.StringValue;
}
