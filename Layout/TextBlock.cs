using System;
using System.Collections.Generic;
using System.Globalization;

namespace InkSharp
{
    public sealed class TextBlock : StyledElement<TextBlock>
    {
        // Reserva de ancho para medir {page}/{pages} antes de conocer el total.
        private const string PageNumberSample = "999999";

        private readonly string _text;
        private readonly bool _isPageNumber;
        private readonly List<TextLine>? _wrapped;
        private readonly int _firstLine;
        private Spacing _padding;
        private PdfColor? _background;
        private PdfColor? _borderColor;
        private double _borderWidth = 0.5;
        private bool _keepTogether;

        internal TextBlock(string text, bool isPageNumber = false)
        {
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _isPageNumber = isPageNumber;
        }

        private TextBlock(TextBlock source, List<TextLine> wrapped, int firstLine)
            : this(source._text, source._isPageNumber)
        {
            _wrapped = wrapped;
            _firstLine = firstLine;
            _padding = source._padding;
            _background = source._background;
            _borderColor = source._borderColor;
            _borderWidth = source._borderWidth;
            LocalStyle = source.LocalStyle;
        }

        public TextBlock Padding(double all) => Padding(all, all, all, all);

        public TextBlock Padding(double vertical, double horizontal) => Padding(vertical, horizontal, vertical, horizontal);

        public TextBlock Padding(double top, double right, double bottom, double left)
        {
            _padding = new Spacing(top, right, bottom, left);
            return this;
        }

        public TextBlock Background(PdfColor color)
        {
            _background = color;
            return this;
        }

        public TextBlock Background(string hex) => Background(PdfColor.FromHex(hex));

        public TextBlock Border(PdfColor color, double width = 0.5)
        {
            _borderColor = color;
            _borderWidth = width;
            return this;
        }

        public TextBlock Border(string hex, double width = 0.5) => Border(PdfColor.FromHex(hex), width);

        /// <summary>Impide que el bloque se divida entre páginas.</summary>
        public TextBlock KeepTogether()
        {
            _keepTogether = true;
            return this;
        }

        internal override LayoutResult Layout(double width, double height, TextStyle inherited, bool force)
        {
            TextStyle style = inherited.Over(LocalStyle);
            PdfFont font = style.ResolveFont();
            double innerWidth = width - _padding.Left - _padding.Right;
            if (innerWidth <= 0)
                throw new LayoutException($"No hay ancho suficiente para el texto \"{Preview()}\".");

            string text = _isPageNumber ? FormatPageNumber(PageNumberSample, PageNumberSample, PageNumberSample, PageNumberSample) : _text;
            List<TextLine> lines = _wrapped ?? Wrap(text, font, style.Size, innerWidth);
            int pending = lines.Count - _firstLine;
            double extra = _padding.Top + _padding.Bottom;

            int fit = double.IsPositiveInfinity(height)
                ? pending
                : (int)Math.Floor((height - extra) / style.Leading + 1e-6);
            if (fit > pending)
                fit = pending;
            if (_keepTogether && fit < pending && !force)
                fit = 0;
            if (fit <= 0)
            {
                if (!force)
                    return LayoutResult.DoesNotFit(this);
                fit = 1;
            }

            int first = _firstLine;
            double blockHeight = fit * style.Leading + extra;
            var fragment = new Fragment(blockHeight, (context, x, y) => Draw(context, x, y, width, blockHeight, lines, first, fit, style, font));
            Element? rest = fit < pending ? new TextBlock(this, lines, _firstLine + fit) : null;
            return new LayoutResult(fragment, rest);
        }

        private void Draw(DrawContext context, double x, double y, double width, double height, List<TextLine> lines, int first, int count, TextStyle style, PdfFont font)
        {
            PdfPage page = context.Page;
            if (_background != null)
                page.DrawRectangle(x, y, width, height, fill: _background);
            if (_borderColor != null)
                page.DrawRectangle(x, y, width, height, stroke: _borderColor, strokeWidth: _borderWidth);

            double innerWidth = width - _padding.Left - _padding.Right;
            if (_isPageNumber)
            {
                string text = FormatPageNumber(
                    context.PageNumber.ToString(CultureInfo.InvariantCulture),
                    context.TotalPages.ToString(CultureInfo.InvariantCulture),
                    context.SectionPage.ToString(CultureInfo.InvariantCulture),
                    context.SectionPages.ToString(CultureInfo.InvariantCulture));
                lines = Wrap(text, font, style.Size, innerWidth);
                first = 0;
                count = lines.Count;
            }

            // Cada línea ocupa el interlineado completo y el texto va centrado verticalmente en él.
            double top = y + _padding.Top + (style.Leading - style.Size) / 2;
            page.DrawLines(lines, first, count, x + _padding.Left, top, innerWidth, style.Align ?? TextAlign.Left, font, style.Size, style.Color ?? PdfColor.Black, style.Leading);
        }

        private string FormatPageNumber(string page, string pages, string sectionPage, string sectionPages) => _text
            .Replace("{sectionPage}", sectionPage)
            .Replace("{sectionPages}", sectionPages)
            .Replace("{page}", page)
            .Replace("{pages}", pages);

        private string Preview() => _text.Length <= 30 ? _text : _text.Substring(0, 30) + "…";

        private static List<TextLine> Wrap(string text, PdfFont font, double size, double width)
        {
            List<TextLine> lines = TextLayout.Wrap(text, font, size, width);
            if (lines.Count == 0)
                lines.Add(new TextLine("", endsParagraph: true));
            return lines;
        }
    }
}
