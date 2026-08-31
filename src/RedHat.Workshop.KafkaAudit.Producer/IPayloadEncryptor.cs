namespace RedHat.Workshop.KafkaAudit.Producer;

/// Permite sustituir el cifrado en los tests sin depender de una llave real.
public interface IPayloadEncryptor
{
    /// <param name="topic">Se autentica como AAD, atando el ciphertext a su tópico de destino.</param>
    string Encrypt(byte[] plaintext, string topic);
}
