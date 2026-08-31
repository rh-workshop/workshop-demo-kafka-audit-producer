using Produbanco.Logs.Host;

namespace Produbanco.Logs.Producer.Tests;

/// Fija la lectura de configuración del host. Lo crítico es el caso del valor mal escrito
/// (RATE_MS=5O, con la letra O): debe ABORTAR, no caer al fallback en silencio — en una prueba de
/// carga, descubrir que la tasa real era la de por defecto cuesta horas.
///
/// Los tests de una misma clase corren en serie en xUnit, así que manipular la variable RATE_MS
/// aquí no interfiere con el resto del ensamblado (nadie más la lee).
public class HostOptionsTests
{
    /// Ejecuta FromEnvironment con RATE_MS puesta a un valor, restaurándola siempre al salir.
    private static HostOptions ConRateMs(string? valor)
    {
        try
        {
            Environment.SetEnvironmentVariable("RATE_MS", valor);
            return HostOptions.FromEnvironment();
        }
        finally
        {
            Environment.SetEnvironmentVariable("RATE_MS", null);
        }
    }

    [Fact]
    public void Sin_variable_usa_el_fallback()
    {
        Assert.Equal(10_000, ConRateMs(null).RateMs);
    }

    [Fact]
    public void Un_entero_valido_se_lee_tal_cual()
    {
        Assert.Equal(250, ConRateMs("250").RateMs);
    }

    /// El clásico dedazo: "5O" con la letra O en vez del cero. TryParse falla y el arranque debe
    /// abortar con la variable y el valor en el mensaje, nunca continuar con el fallback.
    [Fact]
    public void Un_valor_mal_escrito_aborta_en_vez_de_usar_el_fallback()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ConRateMs("5O"));

        Assert.Contains("RATE_MS", ex.Message);
        Assert.Contains("5O", ex.Message);
    }

    /// Una tasa negativa no tiene sentido y casi seguro es otro error de tipeo: también aborta.
    [Fact]
    public void Un_valor_negativo_tambien_aborta()
    {
        Assert.Throws<InvalidOperationException>(() => ConRateMs("-1"));
    }
}
