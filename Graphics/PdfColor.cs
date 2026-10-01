using System;
using System.Globalization;

namespace InkSharp
{
    /// <summary>Color RGB con componentes entre 0 y 1.</summary>
    public readonly struct PdfColor : IEquatable<PdfColor>
    {
        public PdfColor(double r, double g, double b)
        {
            R = Clamp(r);
            G = Clamp(g);
            B = Clamp(b);
        }

        public double R { get; }

        public double G { get; }

        public double B { get; }

        public static PdfColor Black => new PdfColor(0, 0, 0);
        public static PdfColor White => new PdfColor(1, 1, 1);
        public static PdfColor Red => new PdfColor(1, 0, 0);
        public static PdfColor Green => new PdfColor(0, 0.5, 0);
        public static PdfColor Blue => new PdfColor(0, 0, 1);
        public static PdfColor LightGray => FromGray(0.85);
        public static PdfColor DarkGray => FromGray(0.35);

        /// <summary>Color a partir de componentes 0..255.</summary>
        public static PdfColor FromRgb(int r, int g, int b) => new PdfColor(r / 255.0, g / 255.0, b / 255.0);

        /// <summary>Gris: 0 = negro, 1 = blanco.</summary>
        public static PdfColor FromGray(double level) => new PdfColor(level, level, level);

        /// <summary>Acepta "#RGB", "#RRGGBB" (con o sin '#').</summary>
        public static PdfColor FromHex(string hex)
        {
            if (hex == null)
                throw new ArgumentNullException(nameof(hex));

            string value = hex.TrimStart('#');
            if (value.Length == 3)
                value = new string(new[] { value[0], value[0], value[1], value[1], value[2], value[2] });
            if (value.Length != 6
                || !int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
                throw new FormatException("Color hexadecimal inválido: " + hex);

            return FromRgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }

        public bool Equals(PdfColor other) => R == other.R && G == other.G && B == other.B;

        public override bool Equals(object? obj) => obj is PdfColor other && Equals(other);

        public override int GetHashCode() => (R, G, B).GetHashCode();

        public override string ToString() => $"#{(int)Math.Round(R * 255):X2}{(int)Math.Round(G * 255):X2}{(int)Math.Round(B * 255):X2}";

        private static double Clamp(double value) => value < 0 ? 0 : value > 1 ? 1 : value;
    }
}
