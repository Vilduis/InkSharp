using System;
using System.IO;

namespace InkSharp.Core
{
    // Lleva la posición actual para la tabla xref.
    internal sealed class PdfOutput
    {
        private readonly Stream _stream;
        private byte[] _buffer = new byte[256];

        public PdfOutput(Stream stream)
        {
            _stream = stream;
        }

        public long Position { get; private set; }

        public void Write(byte value)
        {
            _stream.WriteByte(value);
            Position++;
        }

        public void Write(byte[] bytes)
        {
            _stream.Write(bytes, 0, bytes.Length);
            Position += bytes.Length;
        }

        /// <summary>Escribe texto ASCII (la sintaxis del PDF es ASCII).</summary>
        public void Write(string ascii)
        {
            // Búfer reutilizado: se llama miles de veces por página.
            if (_buffer.Length < ascii.Length)
                _buffer = new byte[Math.Max(ascii.Length, _buffer.Length * 2)];
            for (int i = 0; i < ascii.Length; i++)
                _buffer[i] = (byte)ascii[i];
            _stream.Write(_buffer, 0, ascii.Length);
            Position += ascii.Length;
        }

        /// <summary>Mismo resultado que <see cref="PdfFormat.Num"/>, sin crear cadenas: es la ruta más usada al dibujar.</summary>
        public void WriteNumber(double value)
        {
            // Fuera de este rango (o NaN/infinito) se usa la versión general, que también valida.
            if (!(Math.Abs(value) < 1e11))
            {
                Write(PdfFormat.Num(value));
                return;
            }

            // Hasta 4 decimales, sin ceros a la derecha: 12.5 → "12.5", 3.0 → "3".
            long scaled = (long)Math.Round(value * 10000);
            int length = 0;
            if (scaled < 0)
            {
                _buffer[length++] = (byte)'-';
                scaled = -scaled;
            }

            long integer = scaled / 10000;
            int fraction = (int)(scaled % 10000);
            int start = length;
            do
            {
                _buffer[length++] = (byte)('0' + integer % 10);
                integer /= 10;
            }
            while (integer > 0);
            Array.Reverse(_buffer, start, length - start);

            if (fraction != 0)
            {
                _buffer[length++] = (byte)'.';
                for (int divisor = 1000; fraction != 0; divisor /= 10)
                {
                    _buffer[length++] = (byte)('0' + fraction / divisor);
                    fraction %= divisor;
                }
            }

            _stream.Write(_buffer, 0, length);
            Position += length;
        }
    }
}
