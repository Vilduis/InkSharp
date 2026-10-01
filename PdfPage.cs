using System;
using System.Collections.Generic;
using InkSharp.Core;

namespace InkSharp
{
    /// <summary>Página de dibujo libre. Origen (0, 0) arriba a la izquierda, medidas en puntos.</summary>
    public sealed class PdfPage
    {
        // Constante para aproximar un cuarto de círculo con una curva de Bézier.
        private const double Kappa = 0.5522847498;

        private readonly PdfDocument _document;
        private readonly ContentBuilder _content = new ContentBuilder();

        internal PdfPage(PdfDocument document, PageSize size)
        {
            _document = document;
            Size = size;
        }

        public PageSize Size { get; }

        public double Width => Size.Width;

        public double Height => Size.Height;

        internal HashSet<PdfFont> UsedFonts { get; } = new HashSet<PdfFont>();

        internal HashSet<PdfImage> UsedImages { get; } = new HashSet<PdfImage>();

        internal byte[] GetContent() => _content.ToArray();

        /// <summary>Una línea de texto; (x, y) es su esquina superior izquierda.</summary>
        public PdfPage DrawText(string text, double x, double y, PdfFont font, double fontSize, PdfColor? color = null)
        {
            ValidateText(text, font, fontSize);
            DrawLines(new[] { new TextLine(text, endsParagraph: true) }, 0, 1, x, y, 0, TextAlign.Left, font, fontSize, color ?? PdfColor.Black, 0);
            return this;
        }

        /// <summary>Texto ajustado al ancho. Devuelve el alto ocupado.</summary>
        public double DrawTextBox(
            string text,
            double x,
            double y,
            double width,
            PdfFont font,
            double fontSize,
            TextAlign align = TextAlign.Left,
            PdfColor? color = null,
            double lineSpacing = 1.2)
        {
            ValidateText(text, font, fontSize);

            List<TextLine> lines = TextLayout.Wrap(text, font, fontSize, width);
            double lineHeight = fontSize * lineSpacing;
            DrawLines(lines, 0, lines.Count, x, y, width, align, font, fontSize, color ?? PdfColor.Black, lineHeight);
            return lines.Count * lineHeight;
        }

        /// <summary>
        /// Líneas ya ajustadas (desde <paramref name="first"/>) en un solo bloque de texto; <paramref name="top"/> es el borde superior de la primera.
        /// Lo usan DrawText, DrawTextBox y los textos del diseño por bloques.
        /// </summary>
        internal void DrawLines(IReadOnlyList<TextLine> lines, int first, int count, double x, double top, double width, TextAlign align, PdfFont font, double fontSize, PdfColor color, double leading)
        {
            if (count == 0)
                return;

            string resource = UseFont(font);
            _content.Line("q");
            _content.Line("BT");
            _content.Raw("/" + resource + " ").Num(fontSize).Line("Tf");
            _content.FillColor(color);

            double currentSpacing = 0;
            for (int i = 0; i < count; i++)
            {
                TextLine line = lines[first + i];
                double lineWidth = font.MeasureText(line.Text, fontSize);
                double lineX = x;
                double wordSpacing = 0;

                switch (align)
                {
                    case TextAlign.Center:
                        lineX += (width - lineWidth) / 2;
                        break;
                    case TextAlign.Right:
                        lineX += width - lineWidth;
                        break;
                    case TextAlign.Justify:
                        int spaces = TextLayout.CountSpaces(line.Text);
                        if (!line.EndsParagraph && spaces > 0)
                            wordSpacing = (width - lineWidth) / spaces;
                        break;
                }

                // Tw se mantiene dentro de BT; solo se escribe cuando cambia. Afecta al byte 32: válido con fuentes de un byte.
                if (wordSpacing != currentSpacing)
                {
                    _content.Num(wordSpacing).Line("Tw");
                    currentSpacing = wordSpacing;
                }
                _content.Raw("1 0 0 1 ").Num(lineX).Num(Baseline(top + i * leading, font, fontSize)).Line("Tm");
                _content.ShowText(font.Encode(line.Text));
            }

            _content.Line("ET");
            _content.Line("Q");
        }

        public PdfPage DrawLine(double x1, double y1, double x2, double y2, PdfColor? color = null, double lineWidth = 1)
        {
            _content.Line("q");
            _content.StrokeColor(color ?? PdfColor.Black);
            _content.Num(lineWidth).Line("w");
            _content.Num(x1).Num(Y(y1)).Raw("m ").Num(x2).Num(Y(y2)).Line("l S");
            _content.Line("Q");
            return this;
        }

        /// <summary>Sin relleno ni borde indicados, dibuja el borde en negro.</summary>
        public PdfPage DrawRectangle(
            double x,
            double y,
            double width,
            double height,
            PdfColor? fill = null,
            PdfColor? stroke = null,
            double strokeWidth = 1,
            double cornerRadius = 0)
        {
            BeginShape(fill, ref stroke, strokeWidth);

            double r = Math.Min(Math.Max(cornerRadius, 0), Math.Min(width, height) / 2);
            if (r <= 0)
            {
                _content.Num(x).Num(Y(y + height)).Num(width).Num(height).Line("re");
            }
            else
            {
                double k = r * Kappa;
                double left = x, right = x + width, top = Y(y), bottom = Y(y + height);
                _content.Num(left + r).Num(top).Line("m");
                _content.Num(right - r).Num(top).Line("l");
                _content.Num(right - r + k).Num(top).Num(right).Num(top - r + k).Num(right).Num(top - r).Line("c");
                _content.Num(right).Num(bottom + r).Line("l");
                _content.Num(right).Num(bottom + r - k).Num(right - r + k).Num(bottom).Num(right - r).Num(bottom).Line("c");
                _content.Num(left + r).Num(bottom).Line("l");
                _content.Num(left + r - k).Num(bottom).Num(left).Num(bottom + r - k).Num(left).Num(bottom + r).Line("c");
                _content.Num(left).Num(top - r).Line("l");
                _content.Num(left).Num(top - r + k).Num(left + r - k).Num(top).Num(left + r).Num(top).Line("c");
                _content.Line("h");
            }

            EndShape(fill, stroke);
            return this;
        }

        /// <summary>Dibuja una elipse centrada en (<paramref name="centerX"/>, <paramref name="centerY"/>).</summary>
        public PdfPage DrawEllipse(
            double centerX,
            double centerY,
            double radiusX,
            double radiusY,
            PdfColor? fill = null,
            PdfColor? stroke = null,
            double strokeWidth = 1)
        {
            BeginShape(fill, ref stroke, strokeWidth);

            double cx = centerX, cy = Y(centerY);
            double kx = radiusX * Kappa, ky = radiusY * Kappa;
            _content.Num(cx + radiusX).Num(cy).Line("m");
            _content.Num(cx + radiusX).Num(cy + ky).Num(cx + kx).Num(cy + radiusY).Num(cx).Num(cy + radiusY).Line("c");
            _content.Num(cx - kx).Num(cy + radiusY).Num(cx - radiusX).Num(cy + ky).Num(cx - radiusX).Num(cy).Line("c");
            _content.Num(cx - radiusX).Num(cy - ky).Num(cx - kx).Num(cy - radiusY).Num(cx).Num(cy - radiusY).Line("c");
            _content.Num(cx + kx).Num(cy - radiusY).Num(cx + radiusX).Num(cy - ky).Num(cx + radiusX).Num(cy).Line("c");
            _content.Line("h");

            EndShape(fill, stroke);
            return this;
        }

        public PdfPage DrawCircle(double centerX, double centerY, double radius, PdfColor? fill = null, PdfColor? stroke = null, double strokeWidth = 1)
        {
            return DrawEllipse(centerX, centerY, radius, radius, fill, stroke, strokeWidth);
        }

        /// <summary>Con solo ancho o solo alto se conserva la proporción.</summary>
        public PdfPage DrawImage(PdfImage image, double x, double y, double? width = null, double? height = null)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            image.Scale(width, height, out double w, out double h);
            UsedImages.Add(image);
            string resource = _document.GetResourceName(image);
            _content.Line("q");
            _content.Num(w).Raw("0 0 ").Num(h).Num(x).Num(Y(y + h)).Line("cm");
            _content.Line("/" + resource + " Do");
            _content.Line("Q");
            return this;
        }

        private void BeginShape(PdfColor? fill, ref PdfColor? stroke, double strokeWidth)
        {
            if (fill == null && stroke == null)
                stroke = PdfColor.Black;

            _content.Line("q");
            if (fill != null)
                _content.FillColor(fill.Value);
            if (stroke != null)
            {
                _content.StrokeColor(stroke.Value);
                _content.Num(strokeWidth).Line("w");
            }
        }

        private void EndShape(PdfColor? fill, PdfColor? stroke)
        {
            // f = rellenar, S = trazar, B = ambos.
            _content.Line(fill != null && stroke != null ? "B" : fill != null ? "f" : "S");
            _content.Line("Q");
        }

        private string UseFont(PdfFont font)
        {
            UsedFonts.Add(font);
            return _document.GetResourceName(font);
        }

        private double Y(double y) => Height - y;

        private double Baseline(double top, PdfFont font, double fontSize) => Height - top - font.Ascent / 1000.0 * fontSize;

        private static void ValidateText(string text, PdfFont font, double fontSize)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            if (font == null)
                throw new ArgumentNullException(nameof(font));
            if (fontSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(fontSize), "El tamaño de fuente debe ser positivo.");
        }
    }
}
