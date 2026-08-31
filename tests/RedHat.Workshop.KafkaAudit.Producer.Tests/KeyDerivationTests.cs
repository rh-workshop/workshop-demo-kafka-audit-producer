using RedHat.Workshop.KafkaAudit.Producer;

namespace RedHat.Workshop.KafkaAudit.Producer.Tests;

/// Congela la derivación de llave: es el contrato con el processor Java, y si cambia sin que el
/// otro extremo cambie igual, todos los mensajes acaban en la DLQ.
public class KeyDerivationTests
{
    /// Secreto de ejemplo con el formato que crea el runbook (`openssl rand -base64 32`).
    private const string Secret = "cmVkaGF0LXdvcmtzaG9wLWFlcy0yNTYta2V5LTAwMDE=";

    [Fact]
    public void La_derivacion_es_determinista()
    {
        byte[] primera = KeyDerivation.DeriveKey(Secret, KeyDerivation.InfoV2);
        byte[] segunda = KeyDerivation.DeriveKey(Secret, KeyDerivation.InfoV2);

        Assert.Equal(primera, segunda);
    }

    [Fact]
    public void Produce_una_llave_de_256_bits()
    {
        Assert.Equal(32, KeyDerivation.DeriveKey(Secret, KeyDerivation.InfoV2).Length);
    }

    /// El `info` separa dominios: rotar su versión da una llave distinta sin tocar el Key Vault.
    [Fact]
    public void Un_info_distinto_da_una_llave_distinta()
    {
        byte[] v1 = KeyDerivation.DeriveKey(Secret, KeyDerivation.InfoV2);
        byte[] v2 = KeyDerivation.DeriveKey(Secret, "redhat-workshop/kafka-audit/aes256gcm/v3");

        Assert.NotEqual(v1, v2);
    }

    /// **Vector de interoperabilidad con Java.** El mismo valor está fijado en `KeyDerivationTest`
    /// del pipeline: si uno de los dos cambia y el otro no, el descifrado deja de funcionar y todo
    /// acaba en la DLQ sin que falle ninguna compilación.
    [Fact]
    public void Vector_de_interoperabilidad_con_java()
    {
        byte[] key = KeyDerivation.DeriveKey(Secret, KeyDerivation.InfoV2);

        Assert.Equal(
            "c7f17f9a0d99f34c31a67381b6d97354764b63ffdf433447e38232fe33987fb6",
            Convert.ToHexString(key).ToLowerInvariant());
    }

    /// Cuando el vault entrega una passphrase en claro (no base64), el IKM son sus bytes UTF-8.
    /// El vector se congela porque Java hace exactamente lo mismo por su excepción de decodificación.
    [Fact]
    public void Fallback_utf8_cuando_no_es_base64()
    {
        byte[] key = KeyDerivation.DeriveKey("passphrase-en-claro-del-vault", KeyDerivation.InfoV2);

        Assert.Equal(
            "b53c1f90685cd72ac94d28d43128de1f67683ce773e9c43b10a70e67188c58b0",
            Convert.ToHexString(key).ToLowerInvariant());
    }

    /// **Vector de interoperabilidad con Java.** Base64.getDecoder de Java LANZA ante un salto de
    /// línea interno y cae al fallback UTF-8; Convert.TryFromBase64String de .NET lo IGNORARÍA y
    /// decodificaría igual, derivando una llave DISTINTA en cada extremo (todo iría a la DLQ sin
    /// pista). Este test congela el comportamiento corregido: whitespace interno = fallback UTF-8.
    [Fact]
    public void Whitespace_interno_usa_el_fallback_utf8_como_java()
    {
        const string conSalto = "cmVkaGF0LXdvcmtzaG9wLWFlcy0y\nNTYta2V5LTAwMDE=";

        byte[] key = KeyDerivation.DeriveKey(conSalto, KeyDerivation.InfoV2);

        // HKDF-SHA256 sobre los bytes UTF-8 del texto tal cual (con el salto de línea incluido).
        Assert.Equal(
            "81c810f29e01a7cfe78666aba558b2d2d2245f32fa09a33b2e926d8369da8247",
            Convert.ToHexString(key).ToLowerInvariant());
        // Y nunca la llave del secreto "limpio": eso sería el comportamiento viejo de .NET.
        Assert.NotEqual(
            KeyDerivation.DeriveKey(Secret, KeyDerivation.InfoV2),
            key);
    }
}
