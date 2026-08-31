using System.Text;

namespace Produbanco.Logs.Producer;

/// Formato binario del payload cifrado: <c>version(1) || keyId(1) || IV(12) || ciphertext || tag(16)</c>.
///
/// La cabecera identifica la llave y permite rotarla: el consumidor conserva la llave vieja en modo
/// solo-descifrado mientras expira la retención del tópico. Va dentro del payload y no en headers de
/// Kafka para ser autocontenido y sobrevivir a copias entre tópicos o clústeres (MirrorMaker).
/// Debe coincidir byte a byte con <c>EncryptedPayload.java</c>.
public static class EncryptedPayload
{
    public const byte Version1 = 1;
    public const int IvLength = 12;
    public const int TagLength = 16;
    public const int HeaderLength = 2;

    public static byte[] Pack(byte version, byte keyId, byte[] iv, byte[] ciphertext, byte[] tag)
    {
        byte[] output = new byte[HeaderLength + iv.Length + ciphertext.Length + tag.Length];
        output[0] = version;
        output[1] = keyId;
        Buffer.BlockCopy(iv, 0, output, HeaderLength, iv.Length);
        Buffer.BlockCopy(ciphertext, 0, output, HeaderLength + iv.Length, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, output, HeaderLength + iv.Length + ciphertext.Length, tag.Length);
        return output;
    }

    /// AAD de GCM: ata el ciphertext a su contexto — copiado a otro tópico, el tag deja de validar.
    /// No viaja en el mensaje: se reconstruye en ambos extremos.
    public static byte[] AssociatedData(string topic, byte version, byte keyId)
    {
        byte[] topicBytes = Encoding.UTF8.GetBytes(topic);
        byte[] aad = new byte[HeaderLength + topicBytes.Length];
        aad[0] = version;
        aad[1] = keyId;
        Buffer.BlockCopy(topicBytes, 0, aad, HeaderLength, topicBytes.Length);
        return aad;
    }
}
