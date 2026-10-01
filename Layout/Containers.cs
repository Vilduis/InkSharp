using System;

namespace InkSharp
{
    /// <summary>Métodos de estilo comunes. El estilo se hereda a los elementos hijos.</summary>
    public abstract class StyledElement<T> : Element
        where T : StyledElement<T>
    {
        internal StyledElement()
        {
        }

        internal TextStyle LocalStyle { get; set; } = new TextStyle();

        public T Style(TextStyle style)
        {
            LocalStyle = LocalStyle.Over(style ?? throw new ArgumentNullException(nameof(style)));
            return Self;
        }

        public T Font(FontFamily font)
        {
            LocalStyle.Font = font;
            return Self;
        }

        public T FontSize(double size)
        {
            if (size <= 0)
                throw new ArgumentOutOfRangeException(nameof(size), "El tamaño de fuente debe ser positivo.");
            LocalStyle.FontSize = size;
            return Self;
        }

        public T Bold(bool value = true)
        {
            LocalStyle.Bold = value;
            return Self;
        }

        public T Italic(bool value = true)
        {
            LocalStyle.Italic = value;
            return Self;
        }

        public T Color(PdfColor color)
        {
            LocalStyle.Color = color;
            return Self;
        }

        public T Color(string hex) => Color(PdfColor.FromHex(hex));

        public T LineHeight(double factor)
        {
            if (factor <= 0)
                throw new ArgumentOutOfRangeException(nameof(factor), "El interlineado debe ser positivo.");
            LocalStyle.LineHeight = factor;
            return Self;
        }

        /// <summary>No se queda solo al final de una página: si lo siguiente no alcanza a empezar, pasa con ello.</summary>
        public T KeepWithNext()
        {
            KeepsWithNext = true;
            return Self;
        }

        public T AlignLeft() => Align(TextAlign.Left);

        public T AlignCenter() => Align(TextAlign.Center);

        public T AlignRight() => Align(TextAlign.Right);

        public T Justify() => Align(TextAlign.Justify);

        private T Align(TextAlign align)
        {
            LocalStyle.Align = align;
            return Self;
        }

        private T Self => (T)this;
    }

    /// <summary>Contenedor al que se le agregan textos, tablas, imágenes, filas y columnas.</summary>
    public abstract class ContainerBase<T> : StyledElement<T>
        where T : ContainerBase<T>
    {
        internal ContainerBase()
        {
        }

        internal abstract void AddChild(Element element);

        public TextBlock Text(string text) => Add(new TextBlock(text));

        /// <summary>Número de página: {page}/{pages} cuentan todo el documento; {sectionPage}/{sectionPages}, solo la sección.</summary>
        public TextBlock PageNumber(string format = "{page}") => Add(new TextBlock(format, isPageNumber: true));

        public ColumnBlock Column(Action<ColumnBlock> build) => Add(Build(new ColumnBlock(), build));

        public RowBlock Row(Action<RowBlock> build) => Add(Build(new RowBlock(), build));

        public TableBlock Table(Action<TableBlock> build) => Add(Build(new TableBlock(), build));

        public ImageBlock Image(PdfImage image) => Add(new ImageBlock(image));

        public ImageBlock Image(string path) => Image(PdfImage.FromFile(path));

        public ImageBlock Image(byte[] data) => Image(PdfImage.FromBytes(data));

        public DividerBlock Divider() => Add(new DividerBlock());

        private TElement Add<TElement>(TElement element)
            where TElement : Element
        {
            AddChild(element);
            return element;
        }

        private static TElement Build<TElement>(TElement element, Action<TElement> build)
        {
            if (build == null)
                throw new ArgumentNullException(nameof(build));
            build(element);
            return element;
        }
    }

    /// <summary>Espacio que admite un único elemento (encabezado, contenido, pie).</summary>
    public sealed class Container : ContainerBase<Container>
    {
        private readonly string _name;
        private Element? _child;

        internal Container(string name)
        {
            _name = name;
        }

        internal bool IsEmpty => _child == null;

        internal override void AddChild(Element element)
        {
            if (_child != null)
                throw new InvalidOperationException(_name + " admite un solo elemento. Usa Column() o Row() para agregar varios.");
            _child = element;
        }

        internal override LayoutResult Layout(double width, double height, TextStyle style, bool force)
            => SingleChild.Layout(_child, LocalStyle, width, height, style, force);
    }
}
