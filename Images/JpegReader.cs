using System.IO;
using InkSharp.Core;

namespace InkSharp.Images
{
    // JPEG se incrusta tal cual (DCTDecode); solo se leen dimensiones y color.
    internal static class JpegReader
    {
        public static bool IsJpeg(byte[] data) => data.Length > 3 && data[0] == 0xFF && data[1] == 0xD8;

        public static PdfImage Read(byte[] data)
        {
            int width = 0, height = 0, components = 0, bitsPerComponent = 8;
            bool adobe = false;
            int i = 2;

            while (i + 4 <= data.Length)
            {
                if (data[i] != 0xFF)
                    throw new InvalidDataException("JPEG corrupto: marcador inválido.");

                byte marker = data[i + 1];
                if (marker == 0xFF)
                {
                    i++;
                    continue;
                }
                if (marker == 0x01 || (marker >= 0xD0 && marker <= 0xD8))
                {
                    i += 2;
                    continue;
                }

                int length = (data[i + 2] << 8) | data[i + 3];

                // APP14 "Adobe": los JPEG CMYK de Adobe guardan los valores invertidos.
                if (marker == 0xEE && length >= 7 && i + 9 < data.Length
                    && data[i + 4] == 'A' && data[i + 5] == 'd' && data[i + 6] == 'o' && data[i + 7] == 'b' && data[i + 8] == 'e')
                    adobe = true;

                bool startOfFrame = marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
                if (startOfFrame)
                {
                    if (i + 9 >= data.Length)
                        break;
                    bitsPerComponent = data[i + 4];
                    height = (data[i + 5] << 8) | data[i + 6];
                    width = (data[i + 7] << 8) | data[i + 8];
                    components = data[i + 9];
                    break;
                }

                i += 2 + length;
            }

            if (width == 0 || height == 0)
                throw new InvalidDataException("No se pudieron leer las dimensiones del JPEG.");

            string colorSpace = components switch
            {
                1 => "DeviceGray",
                3 => "DeviceRGB",
                4 => "DeviceCMYK",
                _ => throw new InvalidDataException("JPEG con " + components + " componentes no soportado."),
            };

            var dictionary = PdfImage.ImageDictionary(width, height, bitsPerComponent)
                .SetName("ColorSpace", colorSpace)
                .SetName("Filter", "DCTDecode");

            if (components == 4 && adobe)
                dictionary.Set("Decode", PdfArray.Numbers(1, 0, 1, 0, 1, 0, 1, 0));

            return new PdfImage(width, height, new PdfStream(dictionary, data));
        }
    }
}
