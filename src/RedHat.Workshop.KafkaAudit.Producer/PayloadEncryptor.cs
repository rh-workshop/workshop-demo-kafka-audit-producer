using System.Security.Cryptography;

namespace RedHat.Workshop.KafkaAudit.Producer;

/// Cifra el payload con AES-256-GCM. Salida: base64 del formato de <see cref="EncryptedPayload"/>.
///
/// El formato y la derivación de clave (<see cref="KeyDerivation"/>) deben coincidir con los del
/// processor Java, que descifra estos mensajes; cambiar cualquiera rompe la interoperabilidad.
/// **Thread-safe**: <see cref="AesGcm"/> no lo es y <c>EmitAsync</c> invita a emitir en
/// concurrencia; el <c>lock</c> interno evita ciphertexts o tags corruptos.
public sealed class PayloadEncryptor : IPayloadEncryptor, IDisposable
{
    private readonly AesGcm _aes;
    private readonly byte _keyId;
    // Serializa el acceso a _aes: más barato que un AesGcm por llamada, y la llave vive solo aquí.
    private readonly object _encryptLock = new();

    /// <param name="secret">Contenido del fichero montado desde el Key Vault.</param>
    /// <param name="keyId">Identifica la llave dentro del payload, para poder rotarla.</param>
    /// <param name="info">Etiqueta de propósito del HKDF; su versión se sube al rotar.</param>
    public PayloadEncryptor(string secret, byte keyId = 1, string? info = null)
    {
        byte[] key = KeyDerivation.DeriveKey(secret, info ?? KeyDerivation.InfoV2);
        _aes = new AesGcm(key, EncryptedPayload.TagLength);
        _keyId = keyId;
        // La llave ya está dentro de AesGcm: no hace falta dejar la copia local en el heap.
        CryptographicOperations.ZeroMemory(key);
    }

    public string Encrypt(byte[] plaintext, string topic)
    {
        // Nonce único por mensaje: repetirlo en GCM permite recuperar el texto en claro y falsificar.
        byte[] iv = RandomNumberGenerator.GetBytes(EncryptedPayload.IvLength);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[EncryptedPayload.TagLength];

        // El tópico se autentica como AAD: el mismo ciphertext copiado a otro tópico no descifra.
        byte[] aad = EncryptedPayload.AssociatedData(topic, EncryptedPayload.Version1, _keyId);
        // Sin esta sección crítica, dos Encrypt simultáneos publican un mensaje indescifrable.
        lock (_encryptLock)
        {
            _aes.Encrypt(iv, plaintext, ciphertext, tag, aad);
        }

        return Convert.ToBase64String(
            EncryptedPayload.Pack(EncryptedPayload.Version1, _keyId, iv, ciphertext, tag));
    }

    public void Dispose() => _aes.Dispose();
}
