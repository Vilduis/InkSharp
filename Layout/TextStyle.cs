namespace InkSharp
{
    public enum FontFamily
    {
        Helvetica,
        Times,
        Courier,
    }

    /// <summary>Estilo de texto. Las propiedades sin valor se heredan del contenedor.</summary>
    public sealed class TextStyle
    {
        public FontFamily? Font { get; set; }
        public double? FontSize { get; set; }
        public bool? Bold { get; set; }
        public bool? Italic { get; set; }
        public PdfColor? Color { get; set; }
        public TextAlign? Align { get; set; }
        public double? LineHeight { get; set; }

        internal static TextStyle Default => new TextStyle
        {
            Font = FontFamily.Helvetica,
            FontSize = 12,
            Bold = false,
            Italic = false,
            Color = PdfColor.Black,
            Align = TextAlign.Left,
            LineHeight = 1.2,
        };

        internal double Size => FontSize ?? 12;

        internal double Leading => Size * (LineHeight ?? 1.2);

        internal TextStyle Over(TextStyle? local)
        {
            if (local == null)
                return this;

            return new TextStyle
            {
                Font = local.Font ?? Font,
                FontSize = local.FontSize ?? FontSize,
                Bold = local.Bold ?? Bold,
                Italic = local.Italic ?? Italic,
                Color = local.Color ?? Color,
                Align = local.Align ?? Align,
                LineHeight = local.LineHeight ?? LineHeight,
            };
        }

        internal PdfFont ResolveFont() => StandardFont.From(Font ?? FontFamily.Helvetica, Bold ?? false, Italic ?? false);
    }
}
