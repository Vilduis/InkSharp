using System.Collections.Generic;
using System.Text;

namespace InkSharp.Core
{
    /// <summary>Objeto base del modelo de objetos PDF (ISO 32000-1, sección 7.3).</summary>
    internal abstract class PdfObject
    {
        public abstract void WriteTo(PdfOutput output);
    }

    internal sealed class PdfName : PdfObject
    {
        public PdfName(string value)
        {
            Value = value;
        }

        public string Value { get; }

        public override void WriteTo(PdfOutput output)
        {
            var sb = new StringBuilder("/");
            foreach (char c in Value)
            {
                if (c < 0x21 || c > 0x7E || "#()<>[]{}/%".IndexOf(c) >= 0)
                    sb.Append('#').Append(((int)c & 0xFF).ToString("X2"));
                else
                    sb.Append(c);
            }
            output.Write(sb.ToString());
        }
    }

    internal sealed class PdfNumber : PdfObject
    {
        public PdfNumber(double value)
        {
            Value = value;
        }

        public double Value { get; }

        public override void WriteTo(PdfOutput output) => output.WriteNumber(Value);
    }

    internal sealed class PdfString : PdfObject
    {
        private readonly byte[] _bytes;
        private readonly bool _hex;

        public PdfString(byte[] bytes, bool hex = false)
        {
            _bytes = bytes;
            _hex = hex;
        }

        // ASCII literal; cualquier otro texto en UTF-16BE con BOM.
        public static PdfString FromText(string text)
        {
            bool ascii = true;
            foreach (char c in text)
            {
                if (c > 127)
                {
                    ascii = false;
                    break;
                }
            }

            if (ascii)
                return new PdfString(Encoding.ASCII.GetBytes(text));

            byte[] utf16 = Encoding.BigEndianUnicode.GetBytes(text);
            var bytes = new byte[utf16.Length + 2];
            bytes[0] = 0xFE;
            bytes[1] = 0xFF;
            utf16.CopyTo(bytes, 2);
            return new PdfString(bytes, hex: true);
        }

        public override void WriteTo(PdfOutput output)
        {
            if (_hex)
                output.Write("<" + PdfFormat.Hex(_bytes) + ">");
            else
                PdfFormat.WriteLiteralString(output, _bytes);
        }
    }

    internal sealed class PdfArray : PdfObject
    {
        private readonly List<PdfObject> _items = new List<PdfObject>();

        public PdfArray(params PdfObject[] items)
        {
            _items.AddRange(items);
        }

        public static PdfArray Numbers(params double[] values)
        {
            var array = new PdfArray();
            foreach (double value in values)
                array.Add(new PdfNumber(value));
            return array;
        }

        public void Add(PdfObject item) => _items.Add(item);

        public override void WriteTo(PdfOutput output)
        {
            output.Write((byte)'[');
            for (int i = 0; i < _items.Count; i++)
            {
                if (i > 0)
                    output.Write((byte)' ');
                _items[i].WriteTo(output);
            }
            output.Write((byte)']');
        }
    }

    internal sealed class PdfDictionary : PdfObject
    {
        // Lista en vez de Dictionary para conservar el orden de inserción en el archivo.
        private readonly List<KeyValuePair<string, PdfObject>> _entries = new List<KeyValuePair<string, PdfObject>>();

        public PdfDictionary Set(string key, PdfObject value)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Key == key)
                {
                    _entries[i] = new KeyValuePair<string, PdfObject>(key, value);
                    return this;
                }
            }
            _entries.Add(new KeyValuePair<string, PdfObject>(key, value));
            return this;
        }

        public PdfDictionary SetName(string key, string name) => Set(key, new PdfName(name));

        public PdfDictionary SetNumber(string key, double value) => Set(key, new PdfNumber(value));

        /// <summary>Copia superficial: permite agregar entradas sin modificar un diccionario compartido.</summary>
        public PdfDictionary Copy()
        {
            var copy = new PdfDictionary();
            copy._entries.AddRange(_entries);
            return copy;
        }

        public override void WriteTo(PdfOutput output)
        {
            output.Write("<<");
            foreach (var entry in _entries)
            {
                output.Write((byte)' ');
                new PdfName(entry.Key).WriteTo(output);
                output.Write((byte)' ');
                entry.Value.WriteTo(output);
            }
            output.Write(" >>");
        }
    }

    internal sealed class PdfReference : PdfObject
    {
        public PdfReference(int number)
        {
            Number = number;
        }

        public int Number { get; }

        public override void WriteTo(PdfOutput output) => output.Write(Number + " 0 R");
    }

    internal sealed class PdfStream : PdfObject
    {
        public PdfStream(PdfDictionary dictionary, byte[] data)
        {
            Dictionary = dictionary;
            Data = data;
        }

        public PdfDictionary Dictionary { get; }

        public byte[] Data { get; }

        /// <summary>Crea un stream comprimido con FlateDecode.</summary>
        public static PdfStream Compressed(PdfDictionary dictionary, byte[] data)
        {
            dictionary.SetName("Filter", "FlateDecode");
            return new PdfStream(dictionary, Zlib.Compress(data));
        }

        // No modifica Dictionary: una misma imagen puede guardarse en varios documentos a la vez.
        public override void WriteTo(PdfOutput output)
        {
            Dictionary.Copy().SetNumber("Length", Data.Length).WriteTo(output);
            output.Write("\nstream\n");
            output.Write(Data);
            output.Write("\nendstream");
        }
    }
}
