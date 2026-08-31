using System.IO.Compression;

namespace Produbanco.Logs.Producer;

/// <summary>
/// Comprime el OTLP ANTES de cifrar: el dato cifrado (AES-256-GCM) es incompresible, por eso el
/// tópico 'encrypted' va con compression.type=none en Kafka. GZip de la BCL, sin librerías de
/// terceros; el mismo formato lo descomprime el processor Java (java.util.zip).
/// </summary>
public static class Compression
{
    public static byte[] Compress(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
        {
            gz.Write(data, 0, data.Length);
        }
        return ms.ToArray();
    }
}
