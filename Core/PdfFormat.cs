using System;
using System.Globalization;

namespace InkSharp.Core
{
    /// <summary>Utilidades de formato según la sintaxis del PDF (ISO 32000).</summary>
    internal static class PdfFormat
    {
        // Siempre con punto decimal, sin importar la cultura del sistema.
        public static string Num(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentException("Los números PDF deben ser finitos.", nameof(value));

            double rounded = Math.Round(value, 4);
            if (rounded == Math.Floor(rounded) && Math.Abs(rounded) < 1e15)
                return ((long)rounded).ToString(CultureInfo.InvariantCulture);
            return rounded.ToString("0.####", CultureInfo.InvariantCulture);
        }

        /// <summary>Fecha en formato PDF: D:AAAAMMDDHHmmSS+HH'mm'.</summary>
        public static string Date(DateTimeOffset date)
        {
            TimeSpan offset = date.Offset;
            char sign = offset < TimeSpan.Zero ? '-' : '+';
            offset = offset.Duration();
            return "D:" + date.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)
                + sign + offset.Hours.ToString("00", CultureInfo.InvariantCulture)
                + "'" + offset.Minutes.ToString("00", CultureInfo.InvariantCulture) + "'";
        }

        /// <summary>Escribe una cadena literal "(...)" escapando los caracteres especiales.</summary>
        public static void WriteLiteralString(PdfOutput output, byte[] bytes)
        {
            output.Write((byte)'(');
            foreach (byte b in bytes)
            {
                switch (b)
                {
                    case (byte)'(':
                    case (byte)')':
                    case (byte)'\\':
                        output.Write((byte)'\\');
                        output.Write(b);
                        break;
                    case (byte)'\r':
                        output.Write("\\r");
                        break;
                    case (byte)'\n':
                        output.Write("\\n");
                        break;
                    default:
                        output.Write(b);
                        break;
                }
            }
            output.Write((byte)')');
        }

        public static string Hex(byte[] bytes)
        {
            var chars = new char[bytes.Length * 2];
            const string digits = "0123456789ABCDEF";
            for (int i = 0; i < bytes.Length; i++)
            {
                chars[i * 2] = digits[bytes[i] >> 4];
                chars[i * 2 + 1] = digits[bytes[i] & 0xF];
            }
            return new string(chars);
        }
    }
}
