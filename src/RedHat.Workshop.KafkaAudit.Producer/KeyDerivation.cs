using System.Security.Cryptography;
using System.Text;

namespace RedHat.Workshop.KafkaAudit.Producer;

/// Deriva la llave AES-256 del secreto montado, con HKDF-SHA256 (RFC 5869, aprobado en NIST
/// SP 800-56C); el parámetro <c>info</c> ata la llave a su propósito. No se usa PBKDF2 ni Argon2
/// porque la entrada ya es aleatoria y no hay entropía que estirar (SP 800-132). La derivación
/// debe ser byte a byte idéntica a <c>KeyDerivation.java</c> o los mensajes no interoperan.
public static class KeyDerivation
{
    /// Etiqueta de propósito y versión. Al rotar la llave se sube la versión aquí y en el Java.
    public const string InfoV2 = "redhat-workshop/kafka-audit/aes256gcm/v2";

    private const int KeyLengthBytes = 32;

    /// Sal fija y pública: HKDF no exige sal secreta, y la separación de dominios ya la da el
    /// <c>info</c>; fijarla mantiene la interoperabilidad sin transportarla en cada mensaje.
    private static readonly byte[] Salt = Encoding.UTF8.GetBytes("redhat-workshop.kafka-audit.salt.v2");

    public static byte[] DeriveKey(string secret, string info) =>
        HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            ikm: InputKeyMaterial(secret),
            outputLength: KeyLengthBytes,
            salt: Salt,
            info: Encoding.UTF8.GetBytes(info));

    /// El secreto se crea con <c>openssl rand -base64 32</c>: el valor montado es la representación
    /// de 32 bytes aleatorios y se decodifica para alimentar HKDF con la entropía real, no con sus
    /// 44 caracteres ASCII. Si no es base64 válido se usan los bytes del texto (passphrase en claro).
    private static byte[] InputKeyMaterial(string secret)
    {
        // Java (Base64.getDecoder) LANZA ante whitespace y cae al fallback UTF-8; .NET lo IGNORA y
        // decodifica igual. Se replica el criterio de Java (whitespace = no base64), o cada extremo
        // derivaría una llave DISTINTA y todos los mensajes acabarían en la DLQ sin ninguna pista.
        if (secret.Any(char.IsWhiteSpace))
        {
            return Encoding.UTF8.GetBytes(secret);
        }

        // Una sola decodificación: el buffer que valida es también el resultado.
        byte[] decoded = new byte[KeyLengthBytes];
        return Convert.TryFromBase64String(secret, decoded, out int written) && written == KeyLengthBytes
            ? decoded
            : Encoding.UTF8.GetBytes(secret);
    }
}
