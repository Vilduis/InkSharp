using System;

namespace InkSharp
{
    /// <summary>Tamaño de página en puntos (1 pt = 1/72 de pulgada).</summary>
    public readonly struct PageSize
    {
        public PageSize(double width, double height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "El tamaño de página debe ser positivo.");
            Width = width;
            Height = height;
        }

        public double Width { get; }

        public double Height { get; }

        public static PageSize A3 => new PageSize(841.89, 1190.55);
        public static PageSize A4 => new PageSize(595.28, 841.89);
        public static PageSize A5 => new PageSize(419.53, 595.28);
        public static PageSize Letter => new PageSize(612, 792);
        public static PageSize Legal => new PageSize(612, 1008);

        public static PageSize FromMillimeters(double width, double height) => new PageSize(Units.Mm(width), Units.Mm(height));

        /// <summary>La misma página en orientación horizontal.</summary>
        public PageSize Landscape() => Width >= Height ? this : new PageSize(Height, Width);

        /// <summary>La misma página en orientación vertical.</summary>
        public PageSize Portrait() => Height >= Width ? this : new PageSize(Height, Width);

        public override string ToString() => $"{Width} x {Height} pt";
    }

    /// <summary>Conversión de unidades a puntos.</summary>
    public static class Units
    {
        public static double Mm(double millimeters) => millimeters * 72.0 / 25.4;

        public static double Cm(double centimeters) => centimeters * 72.0 / 2.54;

        public static double Inch(double inches) => inches * 72.0;
    }
}
