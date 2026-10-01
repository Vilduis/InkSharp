using System.Collections.Generic;

namespace InkSharp.Fonts
{
    // Windows-1252 implementado a mano: netstandard2.0 no lo trae sin paquetes extra.
    internal static class WinAnsiEncoding
    {
        // Caracteres en los bytes 0x80..0x9F; '\0' = sin asignar.
        private static readonly char[] HighTable =
        {
            '€', '\0', '‚', 'ƒ', '„', '…', '†', '‡',
            'ˆ', '‰', 'Š', '‹', 'Œ', '\0', 'Ž', '\0',
            '\0', '‘', '’', '“', '”', '•', '–', '—',
            '˜', '™', 'š', '›', 'œ', '\0', 'ž', 'Ÿ',
        };

        private static readonly Dictionary<char, byte> HighMap = BuildHighMap();

        public const byte Fallback = (byte)'?';

        public static byte Encode(char c)
        {
            if ((c >= 0x20 && c <= 0x7E) || (c >= 0xA0 && c <= 0xFF))
                return (byte)c;
            if (c == '\t')
                return (byte)' ';
            return HighMap.TryGetValue(c, out byte value) ? value : Fallback;
        }

        public static byte[] Encode(string text)
        {
            var bytes = new byte[text.Length];
            for (int i = 0; i < text.Length; i++)
                bytes[i] = Encode(text[i]);
            return bytes;
        }

        /// <summary>Carácter Unicode que representa un byte, o '\0' si no está asignado.</summary>
        public static char ToUnicode(byte b)
        {
            if ((b >= 0x20 && b <= 0x7E) || b >= 0xA0)
                return (char)b;
            if (b >= 0x80 && b <= 0x9F)
                return HighTable[b - 0x80];
            return '\0';
        }

        private static Dictionary<char, byte> BuildHighMap()
        {
            var map = new Dictionary<char, byte>();
            for (int i = 0; i < HighTable.Length; i++)
            {
                if (HighTable[i] != '\0')
                    map[HighTable[i]] = (byte)(0x80 + i);
            }
            return map;
        }
    }
}
