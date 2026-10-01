using System;
using System.Collections.Generic;
using System.Text;

namespace InkSharp
{
    public enum TextAlign
    {
        Left,
        Center,
        Right,
        Justify,
    }

    /// <summary>Ajuste de texto en líneas según un ancho máximo.</summary>
    public static class TextLayout
    {
        /// <summary>Divide el texto en las líneas que caben en <paramref name="maxWidth"/>.</summary>
        public static IReadOnlyList<string> WrapText(string text, PdfFont font, double fontSize, double maxWidth)
        {
            var result = new List<string>();
            foreach (var line in Wrap(text, font, fontSize, maxWidth))
                result.Add(line.Text);
            return result;
        }

        /// <summary>Alto en puntos que ocupará el texto ajustado (útil para paginar).</summary>
        public static double MeasureHeight(string text, PdfFont font, double fontSize, double maxWidth, double lineSpacing = 1.2)
        {
            return Wrap(text, font, fontSize, maxWidth).Count * fontSize * lineSpacing;
        }

        internal static List<TextLine> Wrap(string text, PdfFont font, double fontSize, double maxWidth)
        {
            if (font == null)
                throw new ArgumentNullException(nameof(font));
            if (maxWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxWidth), "El ancho debe ser positivo.");

            var lines = new List<TextLine>();
            if (string.IsNullOrEmpty(text))
                return lines;

            // El ancho de la línea se acumula palabra por palabra (en milésimas de em) en vez de medirla entera cada vez.
            double spaceUnits = font.GetCharWidth(' ');
            var current = new StringBuilder();
            string[] paragraphs = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (string paragraph in paragraphs)
            {
                string[] words = paragraph.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries);
                current.Clear();
                double currentUnits = 0;

                foreach (string original in words)
                {
                    string word = original;
                    double wordUnits = font.MeasureUnits(word);
                    double candidateUnits = current.Length == 0 ? wordUnits : currentUnits + spaceUnits + wordUnits;
                    if (candidateUnits * fontSize / 1000.0 <= maxWidth)
                    {
                        if (current.Length > 0)
                            current.Append(' ');
                        current.Append(word);
                        currentUnits = candidateUnits;
                        continue;
                    }

                    if (current.Length > 0)
                    {
                        lines.Add(new TextLine(current.ToString(), endsParagraph: false));
                        current.Clear();
                    }

                    // Una palabra más ancha que la caja se corta por caracteres.
                    while (word.Length > 1 && font.MeasureText(word, fontSize) > maxWidth)
                    {
                        int fit = FitCharacters(word, font, fontSize, maxWidth);
                        lines.Add(new TextLine(word.Substring(0, fit), endsParagraph: false));
                        word = word.Substring(fit);
                    }
                    current.Append(word);
                    currentUnits = font.MeasureUnits(word);
                }

                lines.Add(new TextLine(current.ToString(), endsParagraph: true));
            }

            return lines;
        }

        private static readonly char[] WordSeparators = { ' ', '\t' };

        internal static int CountSpaces(string text)
        {
            int count = 0;
            foreach (char c in text)
            {
                if (c == ' ')
                    count++;
            }
            return count;
        }

        private static int FitCharacters(string word, PdfFont font, double fontSize, double maxWidth)
        {
            double width = 0;
            for (int i = 0; i < word.Length; i++)
            {
                width += font.GetCharWidth(word[i]) * fontSize / 1000.0;
                if (width > maxWidth)
                    return Math.Max(1, i);
            }
            return word.Length;
        }
    }

    internal readonly struct TextLine
    {
        public TextLine(string text, bool endsParagraph)
        {
            Text = text;
            EndsParagraph = endsParagraph;
        }

        public string Text { get; }

        /// <summary>La última línea de un párrafo no se justifica.</summary>
        public bool EndsParagraph { get; }
    }
}
