using System;
using System.IO;
using InkSharp.Core;
using InkSharp.Images;

namespace InkSharp
{
    /// <summary>Imagen JPEG o PNG. Reutilízala: se guarda una sola vez en el PDF.</summary>
    public sealed class PdfImage
    {
        internal PdfImage(int width, int height, PdfStream xObject, PdfStream? softMask = null)
        {
            Width = width;
            Height = height;
            XObject = xObject;
            SoftMask = softMask;
        }

        /// <summary>Ancho en píxeles.</summary>
        public int Width { get; }

        /// <summary>Alto en píxeles.</summary>
        public int Height { get; }

        internal PdfStream XObject { get; }

        /// <summary>Canal alfa (transparencia), si la imagen lo tiene.</summary>
        internal PdfStream? SoftMask { get; }

        /// <summary>Tamaño en puntos: con un solo lado indicado, el otro conserva la proporción; sin ninguno, un punto por píxel.</summary>
        internal void Scale(double? width, double? height, out double w, out double h)
        {
            if (width.HasValue && height.HasValue)
            {
                w = width.Value;
                h = height.Value;
            }
            else if (width.HasValue)
            {
                w = width.Value;
                h = w * Height / Width;
            }
            else if (height.HasValue)
            {
                h = height.Value;
                w = h * Width / Height;
            }
            else
            {
                w = Width;
                h = Height;
            }
        }

        /// <summary>Entradas comunes del XObject de imagen (JPEG, PNG y su máscara).</summary>
        internal static PdfDictionary ImageDictionary(int width, int height, int bitsPerComponent)
        {
            return new PdfDictionary()
                .SetName("Type", "XObject")
                .SetName("Subtype", "Image")
                .SetNumber("Width", width)
                .SetNumber("Height", height)
                .SetNumber("BitsPerComponent", bitsPerComponent);
        }

        public static PdfImage FromFile(string path) => FromBytes(File.ReadAllBytes(path));

        public static PdfImage FromStream(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            using (var buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);
                return FromBytes(buffer.ToArray());
            }
        }

        public static PdfImage FromBytes(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (JpegReader.IsJpeg(data))
                return JpegReader.Read(data);
            if (PngReader.IsPng(data))
                return PngReader.Read(data);
            throw new NotSupportedException("Formato de imagen no soportado. Usa JPEG o PNG.");
        }
    }
}
