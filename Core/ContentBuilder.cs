using System.IO;

namespace InkSharp.Core
{
    /// <summary>Acumula los operadores del content stream de una página (ISO 32000-1, sección 8 y 9).</summary>
    internal sealed class ContentBuilder
    {
        private readonly MemoryStream _buffer = new MemoryStream();
        private readonly PdfOutput _output;

        public ContentBuilder()
        {
            _output = new PdfOutput(_buffer);
        }

        /// <summary>Escribe un operando numérico seguido de un espacio, sin crear cadenas.</summary>
        public ContentBuilder Num(double value)
        {
            _output.WriteNumber(value);
            _output.Write((byte)' ');
            return this;
        }

        /// <summary>Texto sin salto de línea al final.</summary>
        public ContentBuilder Raw(string operators)
        {
            _output.Write(operators);
            return this;
        }

        public void Line(string operators)
        {
            _output.Write(operators);
            _output.Write((byte)'\n');
        }

        public void FillColor(PdfColor color) => Num(color.R).Num(color.G).Num(color.B).Line("rg");

        public void StrokeColor(PdfColor color) => Num(color.R).Num(color.G).Num(color.B).Line("RG");

        /// <summary>Escribe "(texto) Tj" con los bytes ya codificados por la fuente.</summary>
        public void ShowText(byte[] encoded)
        {
            PdfFormat.WriteLiteralString(_output, encoded);
            _output.Write(" Tj\n");
        }

        public byte[] ToArray() => _buffer.ToArray();
    }
}
