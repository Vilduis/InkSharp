using System;
using System.IO;
using System.Text;
using InkSharp.Core;

namespace InkSharp.Images
{
    // Sin alfa, IDAT se copia tal cual (Predictor 15); con alfa se separa en una SMask.
    internal static class PngReader
    {
        private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        public static bool IsPng(byte[] data)
        {
            if (data.Length < Signature.Length)
                return false;
            for (int i = 0; i < Signature.Length; i++)
            {
                if (data[i] != Signature[i])
                    return false;
            }
            return true;
        }

        public static PdfImage Read(byte[] data)
        {
            int width = 0, height = 0, bitDepth = 0, colorType = -1, interlace = 0;
            byte[]? palette = null;
            var idat = new MemoryStream();

            int pos = Signature.Length;
            while (pos + 8 <= data.Length)
            {
                int length = ReadInt32(data, pos);
                string type = Encoding.ASCII.GetString(data, pos + 4, 4);
                int start = pos + 8;
                if (length < 0 || start + length > data.Length)
                    throw new InvalidDataException("PNG corrupto: chunk truncado.");

                switch (type)
                {
                    case "IHDR":
                        width = ReadInt32(data, start);
                        height = ReadInt32(data, start + 4);
                        bitDepth = data[start + 8];
                        colorType = data[start + 9];
                        interlace = data[start + 12];
                        break;
                    case "PLTE":
                        palette = new byte[length];
                        Array.Copy(data, start, palette, 0, length);
                        break;
                    case "IDAT":
                        idat.Write(data, start, length);
                        break;
                }

                if (type == "IEND")
                    break;
                pos = start + length + 4; // + CRC
            }

            if (width <= 0 || height <= 0)
                throw new InvalidDataException("PNG sin cabecera IHDR válida.");
            if (interlace != 0)
                throw new NotSupportedException("PNG entrelazado (Adam7) no soportado todavía.");

            byte[] compressed = idat.ToArray();

            switch (colorType)
            {
                case 0: // Gris
                    return Direct(width, height, bitDepth, 1, new PdfName("DeviceGray"), compressed);
                case 2: // RGB
                    return Direct(width, height, bitDepth, 3, new PdfName("DeviceRGB"), compressed);
                case 3: // Paleta
                    if (palette == null)
                        throw new InvalidDataException("PNG indexado sin paleta.");
                    var indexed = new PdfArray(
                        new PdfName("Indexed"),
                        new PdfName("DeviceRGB"),
                        new PdfNumber(palette.Length / 3 - 1),
                        new PdfString(palette, hex: true));
                    return Direct(width, height, bitDepth, 1, indexed, compressed);
                case 4: // Gris + alfa
                    return WithAlpha(width, height, bitDepth, 1, "DeviceGray", compressed);
                case 6: // RGB + alfa
                    return WithAlpha(width, height, bitDepth, 3, "DeviceRGB", compressed);
                default:
                    throw new InvalidDataException("Tipo de color PNG desconocido: " + colorType);
            }
        }

        private static PdfImage Direct(int width, int height, int bitDepth, int colors, PdfObject colorSpace, byte[] compressed)
        {
            var decodeParms = new PdfDictionary()
                .SetNumber("Predictor", 15)
                .SetNumber("Colors", colors)
                .SetNumber("BitsPerComponent", bitDepth)
                .SetNumber("Columns", width);

            var dictionary = PdfImage.ImageDictionary(width, height, bitDepth)
                .Set("ColorSpace", colorSpace)
                .SetName("Filter", "FlateDecode")
                .Set("DecodeParms", decodeParms);

            return new PdfImage(width, height, new PdfStream(dictionary, compressed));
        }

        private static PdfImage WithAlpha(int width, int height, int bitDepth, int colors, string colorSpace, byte[] compressed)
        {
            if (bitDepth != 8 && bitDepth != 16)
                throw new NotSupportedException("PNG con alfa de " + bitDepth + " bits no soportado.");

            int sampleBytes = bitDepth / 8;
            int pixelBytes = (colors + 1) * sampleBytes;
            byte[] pixels = Unfilter(Zlib.Decompress(compressed), width, height, pixelBytes);

            int colorBytes = colors * sampleBytes;
            var color = new byte[width * height * colorBytes];
            var alpha = new byte[width * height * sampleBytes];
            for (int p = 0, c = 0, a = 0; p < pixels.Length; p += pixelBytes)
            {
                Array.Copy(pixels, p, color, c, colorBytes);
                Array.Copy(pixels, p + colorBytes, alpha, a, sampleBytes);
                c += colorBytes;
                a += sampleBytes;
            }

            var maskDictionary = PdfImage.ImageDictionary(width, height, bitDepth).SetName("ColorSpace", "DeviceGray");
            var mask = PdfStream.Compressed(maskDictionary, alpha);

            var dictionary = PdfImage.ImageDictionary(width, height, bitDepth).SetName("ColorSpace", colorSpace);
            return new PdfImage(width, height, PdfStream.Compressed(dictionary, color), mask);
        }

        /// <summary>Deshace los filtros por fila de PNG (None, Sub, Up, Average, Paeth).</summary>
        private static byte[] Unfilter(byte[] raw, int width, int height, int pixelBytes)
        {
            int stride = width * pixelBytes;
            if (raw.Length < (stride + 1) * height)
                throw new InvalidDataException("PNG corrupto: datos de imagen incompletos.");

            var output = new byte[stride * height];
            int input = 0;
            for (int y = 0; y < height; y++)
            {
                byte filter = raw[input++];
                int row = y * stride;
                int previous = row - stride;

                for (int x = 0; x < stride; x++)
                {
                    int a = x >= pixelBytes ? output[row + x - pixelBytes] : 0;
                    int b = y > 0 ? output[previous + x] : 0;
                    int c = x >= pixelBytes && y > 0 ? output[previous + x - pixelBytes] : 0;
                    int value = raw[input + x];

                    switch (filter)
                    {
                        case 0: break;
                        case 1: value += a; break;
                        case 2: value += b; break;
                        case 3: value += (a + b) >> 1; break;
                        case 4: value += Paeth(a, b, c); break;
                        default: throw new InvalidDataException("PNG corrupto: filtro " + filter + " desconocido.");
                    }
                    output[row + x] = (byte)value;
                }
                input += stride;
            }
            return output;
        }

        private static int Paeth(int a, int b, int c)
        {
            int p = a + b - c;
            int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            if (pa <= pb && pa <= pc)
                return a;
            return pb <= pc ? b : c;
        }

        private static int ReadInt32(byte[] data, int offset)
        {
            return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
        }
    }
}
