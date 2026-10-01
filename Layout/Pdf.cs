using System;
using System.Collections.Generic;
using System.IO;

namespace InkSharp
{
    /// <summary>
    /// Punto de entrada para componer documentos sin calcular coordenadas:
    /// Pdf.Create().Page(page => ...).Save("archivo.pdf").
    /// </summary>
    public sealed class Pdf
    {
        private readonly List<PageBuilder> _sections = new List<PageBuilder>();
        private readonly PdfDocumentInfo _info = new PdfDocumentInfo();

        private Pdf()
        {
        }

        public static Pdf Create(string? title = null) => new Pdf { _info = { Title = title } };

        public Pdf Title(string title)
        {
            _info.Title = title;
            return this;
        }

        public Pdf Author(string author)
        {
            _info.Author = author;
            return this;
        }

        public Pdf Subject(string subject)
        {
            _info.Subject = subject;
            return this;
        }

        public Pdf Keywords(string keywords)
        {
            _info.Keywords = keywords;
            return this;
        }

        /// <summary>Agrega una sección: páginas con el mismo tamaño, márgenes, encabezado y pie.</summary>
        public Pdf Page(Action<PageBuilder> build)
        {
            if (build == null)
                throw new ArgumentNullException(nameof(build));
            var section = new PageBuilder();
            build(section);
            _sections.Add(section);
            return this;
        }

        public void Save(string path) => ToDocument().Save(path);

        public void Save(Stream stream) => ToDocument().Save(stream);

        public byte[] ToBytes() => ToDocument().ToArray();

        /// <summary>Calcula el diseño y devuelve el documento de bajo nivel.</summary>
        public PdfDocument ToDocument()
        {
            if (_sections.Count == 0)
                throw new InvalidOperationException("El documento no tiene páginas. Usa Page(...) antes de guardar.");

            var planned = new List<PlannedPage>();
            foreach (var section in _sections)
                Paginate(section, planned);

            var document = new PdfDocument();
            document.Info.Title = _info.Title;
            document.Info.Author = _info.Author;
            document.Info.Subject = _info.Subject;
            document.Info.Keywords = _info.Keywords;

            var sectionPages = new Dictionary<PageBuilder, int>();
            foreach (var plan in planned)
                sectionPages[plan.Section] = sectionPages.TryGetValue(plan.Section, out int count) ? count + 1 : 1;

            int sectionPage = 0;
            for (int i = 0; i < planned.Count; i++)
            {
                PlannedPage plan = planned[i];
                PageBuilder section = plan.Section;
                sectionPage = i > 0 && planned[i - 1].Section == section ? sectionPage + 1 : 1;
                PdfPage page = document.AddPage(section.PageSize);
                var context = new DrawContext(page, i + 1, planned.Count, sectionPage, sectionPages[section]);

                plan.Header?.Draw(context, section.Margins.Left, section.Margins.Top);
                plan.Content.Draw(context, section.Margins.Left, plan.ContentTop);
                if (plan.Footer != null)
                    plan.Footer.Draw(context, section.Margins.Left, page.Height - section.Margins.Bottom - plan.Footer.Height);
            }

            return document;
        }

        private static void Paginate(PageBuilder section, List<PlannedPage> planned)
        {
            Spacing margins = section.Margins;
            double width = section.PageSize.Width - margins.Left - margins.Right;
            if (width <= 0)
                throw new LayoutException("Los márgenes laterales ocupan todo el ancho de la página.");

            TextStyle style = TextStyle.Default.Over(section.DefaultStyle);
            Fragment? header = LayoutFixed(section.HeaderSlot, width, style);
            Fragment? footer = LayoutFixed(section.FooterSlot, width, style);

            double top = margins.Top + (header != null ? header.Height + section.Gap : 0);
            double bottom = section.PageSize.Height - margins.Bottom - (footer != null ? footer.Height + section.Gap : 0);
            double available = bottom - top;
            if (available <= 0)
                throw new LayoutException("El encabezado y el pie no dejan espacio para el contenido.");

            Element? pending = section.ContentSlot;
            do
            {
                LayoutResult result = pending.Layout(width, available, style, force: true);
                if (result.Placed == null)
                    throw new LayoutException($"Un elemento no cabe en una página: necesita más de {available:0.#} pt de alto. Divide el contenido o reduce su tamaño.");
                if (result.Placed.Height <= 0 && result.Remaining == pending)
                    throw new LayoutException("El contenido no avanza al paginar.");

                planned.Add(new PlannedPage(section, header, result.Placed, footer, top));
                pending = result.Remaining;
            }
            while (pending != null);
        }

        private static Fragment? LayoutFixed(Container slot, double width, TextStyle style)
        {
            if (slot.IsEmpty)
                return null;
            return slot.Layout(width, double.PositiveInfinity, style, force: false).Placed ?? Fragment.Empty;
        }

        private sealed class PlannedPage
        {
            public PlannedPage(PageBuilder section, Fragment? header, Fragment content, Fragment? footer, double contentTop)
            {
                Section = section;
                Header = header;
                Content = content;
                Footer = footer;
                ContentTop = contentTop;
            }

            public PageBuilder Section { get; }
            public Fragment? Header { get; }
            public Fragment Content { get; }
            public Fragment? Footer { get; }
            public double ContentTop { get; }
        }
    }

    public sealed class PageBuilder
    {
        internal PageBuilder()
        {
        }

        internal PageSize PageSize { get; private set; } = PageSize.A4;
        internal Spacing Margins { get; private set; } = new Spacing(40);
        internal TextStyle DefaultStyle { get; private set; } = new TextStyle();
        internal double Gap { get; private set; } = 12;
        internal Container HeaderSlot { get; } = new Container("El encabezado");
        internal Container ContentSlot { get; } = new Container("El contenido");
        internal Container FooterSlot { get; } = new Container("El pie de página");

        public PageBuilder Size(PageSize size)
        {
            PageSize = size;
            return this;
        }

        public PageBuilder Size(double width, double height) => Size(new PageSize(width, height));

        public PageBuilder Landscape() => Size(PageSize.Landscape());

        /// <summary>Márgenes en puntos (40 por defecto).</summary>
        public PageBuilder Margin(double all) => Margin(all, all, all, all);

        public PageBuilder Margin(double vertical, double horizontal) => Margin(vertical, horizontal, vertical, horizontal);

        public PageBuilder Margin(double top, double right, double bottom, double left)
        {
            Margins = new Spacing(top, right, bottom, left);
            return this;
        }

        public PageBuilder DefaultTextStyle(TextStyle style)
        {
            DefaultStyle = DefaultStyle.Over(style ?? throw new ArgumentNullException(nameof(style)));
            return this;
        }

        /// <summary>Separación entre encabezado, contenido y pie (12 pt por defecto).</summary>
        public PageBuilder SectionGap(double gap)
        {
            if (gap < 0)
                throw new ArgumentOutOfRangeException(nameof(gap), "La separación no puede ser negativa.");
            Gap = gap;
            return this;
        }

        /// <summary>Se repite en todas las páginas de la sección.</summary>
        public Container Header() => HeaderSlot;

        public Container Content() => ContentSlot;

        /// <summary>Se repite en todas las páginas de la sección.</summary>
        public Container Footer() => FooterSlot;
    }
}
