using System;
using System.Globalization;
using System.Text;
using InkSharp.Core;
using InkSharp.Fonts;

namespace InkSharp
{
    /// <summary>Fuentes estándar del PDF (no se incrustan). Texto en WinAnsi.</summary>
    public sealed class StandardFont : PdfFont
    {
        // Anchos de los caracteres 32..126 tomados de los AFM de Adobe.
        private const string HelveticaWidths =
            "278,278,355,556,556,889,667,191,333,333,389,584,278,333,278,278," +
            "556,556,556,556,556,556,556,556,556,556,278,278,584,584,584,556,1015," +
            "667,667,722,722,667,611,778,722,278,500,667,556,833,722,778,667,778,722,667,611,722,667,944,667,667,611," +
            "278,278,278,469,556,333," +
            "556,556,500,556,556,278,556,556,222,222,500,222,833,556,556,556,556,333,500,278,556,500,722,500,500,500," +
            "334,260,334,584";

        private const string HelveticaBoldWidths =
            "278,333,474,556,556,889,722,238,333,333,389,584,278,333,278,278," +
            "556,556,556,556,556,556,556,556,556,556,333,333,584,584,584,611,975," +
            "722,722,722,722,667,611,778,722,278,556,722,611,833,722,778,667,778,722,667,611,722,667,944,667,667,611," +
            "333,278,333,584,556,333," +
            "556,611,556,611,556,333,611,611,278,278,556,278,889,611,611,611,611,389,556,333,611,556,778,556,556,500," +
            "389,280,389,584";

        private const string TimesRomanWidths =
            "250,333,408,500,500,833,778,180,333,333,500,564,250,333,250,278," +
            "500,500,500,500,500,500,500,500,500,500,278,278,564,564,564,444,921," +
            "722,667,667,722,611,556,722,722,333,389,722,611,889,722,722,556,722,667,556,611,722,722,944,722,722,611," +
            "333,278,333,469,500,333," +
            "444,500,444,500,444,333,500,500,278,278,500,278,778,500,500,500,500,333,389,278,500,500,722,500,500,444," +
            "480,200,480,541";

        private const string TimesBoldWidths =
            "250,333,555,500,500,1000,833,278,333,333,500,570,250,333,250,278," +
            "500,500,500,500,500,500,500,500,500,500,333,333,570,570,570,500,930," +
            "722,667,722,722,667,611,778,778,389,500,778,667,944,722,778,611,778,722,556,667,722,722,1000,722,722,667," +
            "333,278,333,581,500,333," +
            "500,556,444,556,444,333,500,556,278,333,556,278,833,556,500,556,556,444,389,333,556,500,722,500,500,444," +
            "394,220,394,520";

        private const string TimesItalicWidths =
            "250,333,420,500,500,833,778,214,333,333,500,675,250,333,250,278," +
            "500,500,500,500,500,500,500,500,500,500,333,333,675,675,675,500,920," +
            "611,611,667,722,611,611,722,722,333,444,667,556,833,667,722,611,722,611,500,556,722,611,833,611,556,556," +
            "389,278,389,422,500,333," +
            "500,500,444,500,444,278,500,500,278,278,444,278,722,500,500,500,500,389,389,278,500,444,667,444,444,389," +
            "400,275,400,541";

        private const string TimesBoldItalicWidths =
            "250,389,555,500,500,833,778,278,333,333,500,570,250,333,250,278," +
            "500,500,500,500,500,500,500,500,500,500,333,333,570,570,570,500,832," +
            "667,667,667,722,667,667,722,778,389,500,667,611,889,722,722,611,722,667,556,611,722,667,889,667,611,611," +
            "333,278,333,570,500,333," +
            "500,500,444,500,444,333,500,556,278,278,500,278,778,556,500,500,500,389,389,278,556,444,667,500,444,389," +
            "348,220,348,570";

        public static readonly StandardFont Helvetica = new StandardFont("Helvetica", 718, -207, HelveticaWidths);
        public static readonly StandardFont HelveticaBold = new StandardFont("Helvetica-Bold", 718, -207, HelveticaBoldWidths);
        public static readonly StandardFont HelveticaOblique = new StandardFont("Helvetica-Oblique", 718, -207, HelveticaWidths);
        public static readonly StandardFont HelveticaBoldOblique = new StandardFont("Helvetica-BoldOblique", 718, -207, HelveticaBoldWidths);
        public static readonly StandardFont TimesRoman = new StandardFont("Times-Roman", 683, -217, TimesRomanWidths);
        public static readonly StandardFont TimesBold = new StandardFont("Times-Bold", 676, -205, TimesBoldWidths);
        public static readonly StandardFont TimesItalic = new StandardFont("Times-Italic", 683, -205, TimesItalicWidths);
        public static readonly StandardFont TimesBoldItalic = new StandardFont("Times-BoldItalic", 699, -205, TimesBoldItalicWidths);
        public static readonly StandardFont Courier = new StandardFont("Courier", 629, -157, null);
        public static readonly StandardFont CourierBold = new StandardFont("Courier-Bold", 629, -157, null);
        public static readonly StandardFont CourierOblique = new StandardFont("Courier-Oblique", 629, -157, null);
        public static readonly StandardFont CourierBoldOblique = new StandardFont("Courier-BoldOblique", 629, -157, null);

        // Ancho de cada byte WinAnsi (0..255).
        private readonly double[] _widths = new double[256];

        private StandardFont(string name, double ascent, double descent, string? asciiWidths)
        {
            Name = name;
            Ascent = ascent;
            Descent = descent;

            if (asciiWidths == null)
            {
                // Courier es monoespaciada.
                for (int i = 0; i < 256; i++)
                    _widths[i] = 600;
                return;
            }

            string[] parts = asciiWidths.Split(',');
            if (parts.Length != 95)
                throw new InvalidOperationException("Tabla de anchos inválida para " + name);
            for (int i = 0; i < 95; i++)
                _widths[32 + i] = double.Parse(parts[i], CultureInfo.InvariantCulture);
            for (int b = 128; b < 256; b++)
                _widths[b] = EstimateWidth(WinAnsiEncoding.ToUnicode((byte)b));
        }

        internal static StandardFont From(FontFamily family, bool bold, bool italic)
        {
            switch (family)
            {
                case FontFamily.Times:
                    return bold ? (italic ? TimesBoldItalic : TimesBold) : (italic ? TimesItalic : TimesRoman);
                case FontFamily.Courier:
                    return bold ? (italic ? CourierBoldOblique : CourierBold) : (italic ? CourierOblique : Courier);
                default:
                    return bold ? (italic ? HelveticaBoldOblique : HelveticaBold) : (italic ? HelveticaOblique : Helvetica);
            }
        }

        public override string Name { get; }

        public override double Ascent { get; }

        public override double Descent { get; }

        internal override double GetCharWidth(char c) => _widths[WinAnsiEncoding.Encode(c)];

        internal override byte[] Encode(string text) => WinAnsiEncoding.Encode(text);

        internal override PdfDictionary CreateFontDictionary()
        {
            return new PdfDictionary()
                .SetName("Type", "Font")
                .SetName("Subtype", "Type1")
                .SetName("BaseFont", Name)
                .SetName("Encoding", "WinAnsiEncoding");
        }

        // Letras acentuadas: ancho de la letra base (á → a).
        private double EstimateWidth(char c)
        {
            double W(char ascii) => _widths[ascii];

            switch (c)
            {
                case '\0': return 0;
                case ' ': return W(' ');
                case '¡': return W('!');
                case '¿': return W('?');
                case '–':
                case '€':
                case '«':
                case '»':
                case '¢':
                case '£':
                case '¥':
                case '§':
                    return W('0');
                case '—':
                case '…':
                case '‰':
                    return 1000;
                case '‘':
                case '’':
                case '‚':
                case '‹':
                case '›':
                    return W(',');
                case '“':
                case '”':
                case '„':
                    return W('"');
                case '•': return 350;
                case '°': return 400;
                case '·': return W('.');
                case '×': return W('+');
                case '÷': return W('+');
                case '©':
                case '®':
                    return W('O');
                case 'ß': return W('b');
                case 'æ':
                case 'œ':
                    return W('m');
                case 'Æ':
                case 'Œ':
                    return W('W');
                case 'ø': return W('o');
                case 'Ø': return W('O');
            }

            string decomposed = c.ToString().Normalize(NormalizationForm.FormD);
            char letter = decomposed[0];
            if (letter != c && letter >= 32 && letter <= 126)
                return W(letter);

            return W('o');
        }
    }
}
