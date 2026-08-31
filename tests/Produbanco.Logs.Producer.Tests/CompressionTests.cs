using System.IO.Compression;
using System.Text;

using Produbanco.Logs.Producer;

namespace Produbanco.Logs.Producer.Tests;

/// El gzip que produce esta clase lo descomprime el processor Java con `java.util.zip`: el formato
/// es estándar, pero conviene fijarlo con un test porque es parte del contrato entre ambos.
public class CompressionTests
{
    [Fact]
    public void Lo_comprimido_se_descomprime_al_original()
    {
        byte[] original = Encoding.UTF8.GetBytes("log de auditoría con acentos y ñ");

        byte[] resultado = Decompress(Compression.Compress(original));

        Assert.Equal(original, resultado);
    }

    [Fact]
    public void Un_texto_repetitivo_ocupa_menos_comprimido()
    {
        byte[] repetitivo = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("produbanco", 500)));

        Assert.True(Compression.Compress(repetitivo).Length < repetitivo.Length);
    }

    private static byte[] Decompress(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }
}
