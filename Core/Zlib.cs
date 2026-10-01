using System;
using System.IO;
using System.IO.Compression;

namespace InkSharp.Core
{
    // FlateDecode espera zlib: cabecera + deflate + Adler-32.
    internal static class Zlib
    {
        public static byte[] Compress(byte[] data)
        {
            using (var buffer = new MemoryStream())
            {
                buffer.WriteByte(0x78);
                buffer.WriteByte(0x9C);
                using (var deflate = new DeflateStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
                    deflate.Write(data, 0, data.Length);

                uint adler = Adler32(data);
                buffer.WriteByte((byte)(adler >> 24));
                buffer.WriteByte((byte)(adler >> 16));
                buffer.WriteByte((byte)(adler >> 8));
                buffer.WriteByte((byte)adler);
                return buffer.ToArray();
            }
        }

        public static byte[] Decompress(byte[] zlibData)
        {
            // Se salta la cabecera de 2 bytes; el checksum final lo ignora DeflateStream.
            using (var input = new MemoryStream(zlibData, 2, zlibData.Length - 2))
            using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                deflate.CopyTo(output);
                return output.ToArray();
            }
        }

        private static uint Adler32(byte[] data)
        {
            const uint mod = 65521;
            // 5552 es el máximo de bytes que se pueden sumar sin desbordar un uint antes de aplicar el módulo.
            const int block = 5552;
            uint a = 1, b = 0;
            int i = 0;
            while (i < data.Length)
            {
                int end = Math.Min(data.Length, i + block);
                for (; i < end; i++)
                {
                    a += data[i];
                    b += a;
                }
                a %= mod;
                b %= mod;
            }
            return (b << 16) | a;
        }
    }
}
