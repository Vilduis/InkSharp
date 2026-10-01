using InkSharp.Core;

namespace InkSharp
{
    /// <summary>Fuente utilizable para dibujar texto en una página.</summary>
    public abstract class PdfFont
    {
        // Constructor interno: por ahora solo la librería define tipos de fuente.
        internal PdfFont()
        {
        }

        /// <summary>Nombre PostScript de la fuente (p. ej. "Helvetica-Bold").</summary>
        public abstract string Name { get; }

        /// <summary>Altura sobre la línea base, en milésimas de em.</summary>
        public abstract double Ascent { get; }

        /// <summary>Profundidad bajo la línea base (negativa), en milésimas de em.</summary>
        public abstract double Descent { get; }

        /// <summary>Ancho de un carácter en milésimas de em.</summary>
        internal abstract double GetCharWidth(char c);

        internal abstract byte[] Encode(string text);

        internal abstract PdfDictionary CreateFontDictionary();

        /// <summary>Ancho en puntos que ocupa <paramref name="text"/> con el tamaño indicado.</summary>
        public double MeasureText(string text, double fontSize) => MeasureUnits(text) * fontSize / 1000.0;

        /// <summary>Ancho del texto en milésimas de em.</summary>
        internal double MeasureUnits(string text)
        {
            double total = 0;
            foreach (char c in text)
                total += GetCharWidth(c);
            return total;
        }

        public override string ToString() => Name;
    }
}
