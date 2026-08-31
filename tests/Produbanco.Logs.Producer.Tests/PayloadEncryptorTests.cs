using System.Security.Cryptography;
using System.Text;

using Produbanco.Logs.Producer;

namespace Produbanco.Logs.Producer.Tests;

/// Cubre el cifrado del payload: el formato binario es el contrato con el processor Java, así que
/// aquí se descifra "a mano" replicando lo que hace el otro extremo.
public class PayloadEncryptorTests
{
    private const string Secret = "cHJvZHViYW5jby1sYWItYWVzLTI1Ni1rZXktMDAwMDE=";
    private const string Topic = "tp.observability.logs.encrypted";
    private const byte KeyId = 1;

    [Fact]
    public void El_payload_lleva_cabecera_de_version_y_key_id()
    {
        using var encryptor = new PayloadEncryptor(Secret, KeyId);

        byte[] raw = Convert.FromBase64String(encryptor.Encrypt(Payload("dato"), Topic));

        Assert.Equal(EncryptedPayload.Version1, raw[0]);
        Assert.Equal(KeyId, raw[1]);
    }

    /// El tamaño exacto delata el layout: cabecera(2) + IV(12) + ciphertext(n) + tag(16).
    [Fact]
    public void El_tamano_del_payload_corresponde_al_layout_acordado()
    {
        using var encryptor = new PayloadEncryptor(Secret, KeyId);
        byte[] plaintext = Payload("un mensaje de auditoría");

        byte[] raw = Convert.FromBase64String(encryptor.Encrypt(plaintext, Topic));

        Assert.Equal(
            EncryptedPayload.HeaderLength + EncryptedPayload.IvLength
                + plaintext.Length + EncryptedPayload.TagLength,
            raw.Length);
    }

    /// Round-trip completo replicando el descifrado del Java: si este test pasa, el processor
    /// también puede leer el mensaje.
    [Fact]
    public void El_mensaje_se_descifra_con_la_misma_llave_y_aad()
    {
        using var encryptor = new PayloadEncryptor(Secret, KeyId);
        byte[] plaintext = Payload("log de auditoría con acentos y ñ");

        byte[] descifrado = DecryptAsJavaWould(encryptor.Encrypt(plaintext, Topic), Topic);

        Assert.Equal(plaintext, descifrado);
    }

    /// El tópico va como AAD: un ciphertext válido copiado a otro tópico no debe descifrar.
    [Fact]
    public void Un_mensaje_movido_a_otro_topico_no_descifra()
    {
        using var encryptor = new PayloadEncryptor(Secret, KeyId);
        string cifrado = encryptor.Encrypt(Payload("dato"), Topic);

        Assert.Throws<AuthenticationTagMismatchException>(
            () => DecryptAsJavaWould(cifrado, "tp.observability.logs.masked"));
    }

    /// Nonce distinto por mensaje: dos cifrados del mismo texto no pueden coincidir.
    [Fact]
    public void El_mismo_texto_produce_ciphertexts_distintos()
    {
        using var encryptor = new PayloadEncryptor(Secret, KeyId);
        byte[] plaintext = Payload("mismo mensaje");

        Assert.NotEqual(encryptor.Encrypt(plaintext, Topic), encryptor.Encrypt(plaintext, Topic));
    }

    /// Manipular el ciphertext tiene que fallar la autenticación, no devolver basura.
    [Fact]
    public void Un_ciphertext_manipulado_no_descifra()
    {
        using var encryptor = new PayloadEncryptor(Secret, KeyId);
        byte[] raw = Convert.FromBase64String(encryptor.Encrypt(Payload("dato"), Topic));
        raw[^1] ^= 0x01;

        Assert.Throws<AuthenticationTagMismatchException>(
            () => DecryptAsJavaWould(Convert.ToBase64String(raw), Topic));
    }

    private static byte[] Payload(string text) => Encoding.UTF8.GetBytes(text);

    /// Descifra igual que el processor Java: lee la cabecera, reconstruye la AAD y valida el tag.
    private static byte[] DecryptAsJavaWould(string base64, string topic)
    {
        byte[] raw = Convert.FromBase64String(base64);
        byte version = raw[0];
        byte keyId = raw[1];

        byte[] iv = raw[EncryptedPayload.HeaderLength..(EncryptedPayload.HeaderLength + EncryptedPayload.IvLength)];
        int cipherStart = EncryptedPayload.HeaderLength + EncryptedPayload.IvLength;
        int cipherLength = raw.Length - cipherStart - EncryptedPayload.TagLength;
        byte[] ciphertext = raw[cipherStart..(cipherStart + cipherLength)];
        byte[] tag = raw[^EncryptedPayload.TagLength..];

        byte[] key = KeyDerivation.DeriveKey(Secret, KeyDerivation.InfoV1);
        using var aes = new AesGcm(key, EncryptedPayload.TagLength);
        byte[] plaintext = new byte[ciphertext.Length];
        aes.Decrypt(iv, ciphertext, tag, plaintext,
            EncryptedPayload.AssociatedData(topic, version, keyId));
        return plaintext;
    }
}
