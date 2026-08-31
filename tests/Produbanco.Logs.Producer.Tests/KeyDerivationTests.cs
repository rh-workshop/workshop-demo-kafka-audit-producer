using Produbanco.Logs.Producer;

namespace Produbanco.Logs.Producer.Tests;

/// Congela la derivación de llave: es el contrato con el processor Java, y si cambia sin que el
/// otro extremo cambie igual, todos los mensajes acaban en la DLQ.
public class KeyDerivationTests
{
    /// Secreto de ejemplo con el formato que crea el runbook (`openssl rand -base64 32`).
    private const string Secret = "cHJvZHViYW5jby1sYWItYWVzLTI1Ni1rZXktMDAwMDE=";

    [Fact]
    public void La_derivacion_es_determinista()
    {
        byte[] primera = KeyDerivation.DeriveKey(Secret, KeyDerivation.InfoV1);
        byte[] segunda = KeyDerivation.DeriveKey(Secret, KeyDerivation.InfoV1);

        Assert.Equal(primera, segunda);
    }

    [Fact]
    public void Produce_una_llave_de_256_bits()
    {
        Assert.Equal(32, KeyDerivation.DeriveKey(Secret, KeyDerivation.InfoV1).Length);
    }

    /// El `info` separa dominios: rotar su versión da una llave distinta sin tocar el Key Vault.
    [Fact]
    public void Un_info_distinto_da_una_llave_distinta()
    {
        byte[] v1 = KeyDerivation.DeriveKey(Secret, KeyDerivation.InfoV1);
        byte[] v2 = KeyDerivation.DeriveKey(Secret, "produbanco/audit-log/aes256gcm/v2");

        Assert.NotEqual(v1, v2);
    }

    /// **Vector de interoperabilidad con Java.** El mismo valor está fijado en `KeyDerivationTest`
    /// del pipeline: si uno de los dos cambia y el otro no, el descifrado deja de funcionar y todo
    /// acaba en la DLQ sin que falle ninguna compilación.
    [Fact]
    public void Vector_de_interoperabilidad_con_java()
    {
        byte[] key = KeyDerivation.DeriveKey(Secret, KeyDerivation.InfoV1);

        Assert.Equal(
            "c17fbdc63c0d2f384ba94aafa0782a9f7e11b0b7d11fe421f39f938927b15cbd",
            Convert.ToHexString(key).ToLowerInvariant());
    }

    /// Cuando el vault entrega una passphrase en claro (no base64), el IKM son sus bytes UTF-8.
    /// El vector se congela porque Java hace exactamente lo mismo por su excepción de decodificación.
    [Fact]
    public void Fallback_utf8_cuando_no_es_base64()
    {
        byte[] key = KeyDerivation.DeriveKey("passphrase-en-claro-del-vault", KeyDerivation.InfoV1);

        Assert.Equal(
            "9aa31bce942588ea3ede5430bdd604460647912b568ad1aee1005697e98d399d",
            Convert.ToHexString(key).ToLowerInvariant());
    }

    /// **Vector de interoperabilidad con Java.** Base64.getDecoder de Java LANZA ante un salto de
    /// línea interno y cae al fallback UTF-8; Convert.TryFromBase64String de .NET lo IGNORARÍA y
    /// decodificaría igual, derivando una llave DISTINTA en cada extremo (todo iría a la DLQ sin
    /// pista). Este test congela el comportamiento corregido: whitespace interno = fallback UTF-8.
    [Fact]
    public void Whitespace_interno_usa_el_fallback_utf8_como_java()
    {
        const string conSalto = "cHJvZHViYW5jby1sYWItYWVz\nLTI1Ni1rZXktMDAwMDE=";

        byte[] key = KeyDerivation.DeriveKey(conSalto, KeyDerivation.InfoV1);

        // HKDF-SHA256 sobre los bytes UTF-8 del texto tal cual (con el salto de línea incluido).
        Assert.Equal(
            "3ad6a83b33dde6e068bcd42e3647f9b3550763d735e9919306c61081a7068cb7",
            Convert.ToHexString(key).ToLowerInvariant());
        // Y nunca la llave del secreto "limpio": eso sería el comportamiento viejo de .NET.
        Assert.NotEqual(
            KeyDerivation.DeriveKey(Secret, KeyDerivation.InfoV1),
            key);
    }
}
