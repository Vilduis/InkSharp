using System;
using System.Collections.Generic;
using System.IO;
using InkSharp.Core;

namespace InkSharp
{
    /// <summary>Metadatos del documento (aparecen en las propiedades del visor).</summary>
    public sealed class PdfDocumentInfo
    {
        public string? Title { get; set; }
        public string? Author { get; set; }
        public string? Subject { get; set; }
        public string? Keywords { get; set; }
        public string? Creator { get; set; }
        public string Producer { get; set; } = "InkSharp";
        public DateTimeOffset CreationDate { get; set; } = DateTimeOffset.Now;
    }

    /// <summary>Documento de bajo nivel: páginas con dibujo por coordenadas.</summary>
    public sealed class PdfDocument
    {
        private readonly List<PdfPage> _pages = new List<PdfPage>();
        private readonly Dictionary<PdfFont, string> _fontNames = new Dictionary<PdfFont, string>();
        private readonly Dictionary<PdfImage, string> _imageNames = new Dictionary<PdfImage, string>();

        public PdfDocumentInfo Info { get; } = new PdfDocumentInfo();

        /// <summary>Tamaño usado por <see cref="AddPage()"/> sin argumentos. Por defecto A4.</summary>
        public PageSize DefaultPageSize { get; set; } = PageSize.A4;

        /// <summary>Comprime el contenido de las páginas (FlateDecode). Activado por defecto.</summary>
        public bool CompressContent { get; set; } = true;

        public IReadOnlyList<PdfPage> Pages => _pages;

        public PdfPage AddPage() => AddPage(DefaultPageSize);

        public PdfPage AddPage(PageSize size)
        {
            var page = new PdfPage(this, size);
            _pages.Add(page);
            return page;
        }

        public void Save(string path)
        {
            using (var file = new FileStream(path, FileMode.Create, FileAccess.Write))
                Save(file);
        }

        public byte[] ToArray()
        {
            using (var buffer = new MemoryStream())
            {
                Save(buffer);
                return buffer.ToArray();
            }
        }

        public void Save(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (_pages.Count == 0)
                throw new InvalidOperationException("El documento no tiene páginas. Usa AddPage() antes de guardar.");

            var objects = new ObjectTable();
            PdfReference catalogRef = objects.Reserve();
            PdfReference pagesRef = objects.Reserve();

            var fontRefs = new Dictionary<PdfFont, PdfReference>();
            foreach (var font in _fontNames.Keys)
                fontRefs[font] = objects.Add(font.CreateFontDictionary());

            var imageRefs = new Dictionary<PdfImage, PdfReference>();
            foreach (var image in _imageNames.Keys)
            {
                // La referencia a la máscara es propia de este archivo: se agrega a una copia, no a la imagen compartida.
                PdfStream xObject = image.XObject;
                if (image.SoftMask != null)
                    xObject = new PdfStream(xObject.Dictionary.Copy().Set("SMask", objects.Add(image.SoftMask)), xObject.Data);
                imageRefs[image] = objects.Add(xObject);
            }

            var kids = new PdfArray();
            foreach (var page in _pages)
            {
                byte[] content = page.GetContent();
                PdfStream contentStream = CompressContent
                    ? PdfStream.Compressed(new PdfDictionary(), content)
                    : new PdfStream(new PdfDictionary(), content);
                PdfReference contentRef = objects.Add(contentStream);

                var pageDictionary = new PdfDictionary()
                    .SetName("Type", "Page")
                    .Set("Parent", pagesRef)
                    .Set("MediaBox", PdfArray.Numbers(0, 0, page.Width, page.Height))
                    .Set("Resources", BuildResources(page, fontRefs, imageRefs))
                    .Set("Contents", contentRef);
                kids.Add(objects.Add(pageDictionary));
            }

            objects.Set(pagesRef, new PdfDictionary()
                .SetName("Type", "Pages")
                .Set("Kids", kids)
                .SetNumber("Count", _pages.Count));

            objects.Set(catalogRef, new PdfDictionary()
                .SetName("Type", "Catalog")
                .Set("Pages", pagesRef));

            PdfReference infoRef = objects.Add(BuildInfo());

            WriteFile(new PdfOutput(stream), objects, catalogRef, infoRef);
            stream.Flush();
        }

        internal string GetResourceName(PdfFont font)
        {
            if (!_fontNames.TryGetValue(font, out string? name))
            {
                name = "F" + (_fontNames.Count + 1);
                _fontNames[font] = name;
            }
            return name;
        }

        internal string GetResourceName(PdfImage image)
        {
            if (!_imageNames.TryGetValue(image, out string? name))
            {
                name = "Im" + (_imageNames.Count + 1);
                _imageNames[image] = name;
            }
            return name;
        }

        private PdfDictionary BuildResources(
            PdfPage page,
            Dictionary<PdfFont, PdfReference> fontRefs,
            Dictionary<PdfImage, PdfReference> imageRefs)
        {
            var resources = new PdfDictionary();

            if (page.UsedFonts.Count > 0)
            {
                var fonts = new PdfDictionary();
                foreach (var font in page.UsedFonts)
                    fonts.Set(_fontNames[font], fontRefs[font]);
                resources.Set("Font", fonts);
            }

            if (page.UsedImages.Count > 0)
            {
                var xObjects = new PdfDictionary();
                foreach (var image in page.UsedImages)
                    xObjects.Set(_imageNames[image], imageRefs[image]);
                resources.Set("XObject", xObjects);
            }

            return resources;
        }

        private PdfDictionary BuildInfo()
        {
            var info = new PdfDictionary();
            void SetText(string key, string? value)
            {
                if (!string.IsNullOrEmpty(value))
                    info.Set(key, PdfString.FromText(value!));
            }

            SetText("Title", Info.Title);
            SetText("Author", Info.Author);
            SetText("Subject", Info.Subject);
            SetText("Keywords", Info.Keywords);
            SetText("Creator", Info.Creator);
            SetText("Producer", Info.Producer);
            info.Set("CreationDate", PdfString.FromText(PdfFormat.Date(Info.CreationDate)));
            return info;
        }

        /// <summary>Estructura del archivo: cabecera, cuerpo, tabla xref y trailer (ISO 32000-1, 7.5).</summary>
        private static void WriteFile(PdfOutput output, ObjectTable objects, PdfReference catalogRef, PdfReference infoRef)
        {
            output.Write("%PDF-1.7\n");
            // Comentario con bytes > 127: indica a las herramientas que el archivo es binario.
            output.Write(new byte[] { (byte)'%', 0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n' });

            var offsets = new long[objects.Count];
            for (int i = 0; i < objects.Count; i++)
            {
                offsets[i] = output.Position;
                output.Write((i + 1) + " 0 obj\n");
                objects[i].WriteTo(output);
                output.Write("\nendobj\n");
            }

            long xrefOffset = output.Position;
            output.Write("xref\n0 " + (objects.Count + 1) + "\n");
            output.Write("0000000000 65535 f\r\n");
            foreach (long offset in offsets)
                output.Write(offset.ToString("D10", System.Globalization.CultureInfo.InvariantCulture) + " 00000 n\r\n");

            byte[] id = Guid.NewGuid().ToByteArray();
            var trailer = new PdfDictionary()
                .SetNumber("Size", objects.Count + 1)
                .Set("Root", catalogRef)
                .Set("Info", infoRef)
                .Set("ID", new PdfArray(new PdfString(id, hex: true), new PdfString(id, hex: true)));

            output.Write("trailer\n");
            trailer.WriteTo(output);
            output.Write("\nstartxref\n" + xrefOffset + "\n%%EOF\n");
        }

        /// <summary>Objetos indirectos numerados desde 1.</summary>
        private sealed class ObjectTable
        {
            private readonly List<PdfObject?> _objects = new List<PdfObject?>();

            public int Count => _objects.Count;

            public PdfObject this[int index] =>
                _objects[index] ?? throw new InvalidOperationException("Objeto " + (index + 1) + " reservado pero nunca asignado.");

            public PdfReference Add(PdfObject value)
            {
                _objects.Add(value);
                return new PdfReference(_objects.Count);
            }

            public PdfReference Reserve()
            {
                _objects.Add(null);
                return new PdfReference(_objects.Count);
            }

            public void Set(PdfReference reference, PdfObject value) => _objects[reference.Number - 1] = value;
        }
    }
}
