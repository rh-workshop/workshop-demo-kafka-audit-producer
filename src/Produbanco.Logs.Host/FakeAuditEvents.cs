using System.Security.Cryptography;

using Produbanco.Logs.Producer;

namespace Produbanco.Logs.Host;

/// Genera eventos ficticios para la demo. En producción los emiten los microservicios del Banco.
///
/// Vive en el host, no en la biblioteca: la biblioteca que se distribuye como paquete no debe
/// arrastrar datos de prueba.
public sealed class FakeAuditEvents(string environment, int payloadBytes)
{
    private static readonly string[] Customers =
        ["juan.perez", "maria.gomez", "carlos.ruiz", "ana.torres", "luis.vaca"];

    private static readonly string[] Services =
        ["bff-canal", "escenario-negocio", "servicio-dominio", "acceso-core"];

    private const string MailDomain = "@produbanco.com";
    private const string IdFormat = "NET-{0:D6}";
    private const string Channel = "web";

    /// Importe máximo del movimiento ficticio, en centavos.
    private const int MaxAmountCents = 500_000;

    /// Cédula ecuatoriana ficticia: prefijo + 2 dígitos variables + sufijo = 10 dígitos.
    private const string DniPrefix = "17123";
    private const string DniSuffix = "678";

    /// PAN ficticio de 16 dígitos: prefijo de prueba + 4 dígitos variables.
    private const string PanPrefix = "453912345678";

    private readonly string _instanceId = Guid.NewGuid().ToString();
    private long _sequence;

    public AuditEvent Next()
    {
        _sequence++;
        // El módulo va contra la longitud del array, no contra un número fijo, para no desincronizarse.
        return new AuditEvent(
            Id: string.Format(IdFormat, _sequence),
            ServiceName: Services[_sequence % Services.Length],
            ServiceInstanceId: _instanceId,
            Environment: environment,
            Email: Customers[_sequence % Customers.Length] + MailDomain,
            Dni: FakeDni(),
            Pan: FakePan(),
            Amount: Math.Round(RandomNumberGenerator.GetInt32(0, MaxAmountCents) / 100.0, 2),
            Channel: Channel,
            PayloadBytes: payloadBytes);
    }

    private static string FakeDni() =>
        DniPrefix + RandomNumberGenerator.GetInt32(10, 99) + DniSuffix;

    private static string FakePan() =>
        PanPrefix + RandomNumberGenerator.GetInt32(1000, 9999);
}
