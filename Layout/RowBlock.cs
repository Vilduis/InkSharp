using System;
using System.Collections.Generic;
using System.Globalization;

namespace InkSharp
{
    /// <summary>
    /// Ancho de una columna o de un elemento de fila: un número es un ancho fijo en puntos;
    /// "*" o "2*" reparten el espacio restante de forma proporcional.
    /// </summary>
    public readonly struct ColumnSize
    {
        private ColumnSize(double fixedWidth, double weight)
        {
            FixedWidth = fixedWidth;
            Weight = weight;
        }

        internal double FixedWidth { get; }

        internal double Weight { get; }

        public static ColumnSize Fixed(double width)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "El ancho debe ser positivo.");
            return new ColumnSize(width, 0);
        }

        public static ColumnSize Relative(double weight = 1)
        {
            if (weight <= 0)
                throw new ArgumentOutOfRangeException(nameof(weight), "El peso debe ser positivo.");
            return new ColumnSize(0, weight);
        }

        public static implicit operator ColumnSize(double width) => Fixed(width);

        public static implicit operator ColumnSize(string value)
        {
            string text = (value ?? "").Trim();
            if (text == "*")
                return Relative();
            if (text.EndsWith("*") && double.TryParse(text.Substring(0, text.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out double weight))
                return Relative(weight);
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double width))
                return Fixed(width);
            throw new FormatException($"Ancho inválido: \"{value}\". Usa un número, \"*\" o \"2*\".");
        }

        internal static double[] Distribute(IReadOnlyList<ColumnSize> sizes, double total)
        {
            double fixedSum = 0, weightSum = 0;
            foreach (var size in sizes)
            {
                fixedSum += size.FixedWidth;
                weightSum += size.Weight;
            }

            if (fixedSum > total + 1e-6)
                throw new LayoutException($"Los anchos fijos suman {fixedSum:0.##} pt y solo hay {total:0.##} pt disponibles.");

            double free = total - fixedSum;
            var widths = new double[sizes.Count];
            for (int i = 0; i < sizes.Count; i++)
                widths[i] = sizes[i].Weight > 0 ? free * sizes[i].Weight / weightSum : sizes[i].FixedWidth;
            return widths;
        }
    }

    /// <summary>
    /// Coloca elementos uno al lado del otro. Una fila que cabe en una página no se parte;
    /// si es más alta que una página entera, cada elemento continúa en la siguiente.
    /// </summary>
    public sealed class RowBlock : StyledElement<RowBlock>
    {
        private readonly List<RowItem> _items;
        private double _gap = 12;

        internal RowBlock()
            : this(new List<RowItem>())
        {
        }

        private RowBlock(List<RowItem> items)
        {
            _items = items;
        }

        public RowBlock Gap(double gap)
        {
            if (gap < 0)
                throw new ArgumentOutOfRangeException(nameof(gap), "La separación no puede ser negativa.");
            _gap = gap;
            return this;
        }

        public RowItem Item() => Item(ColumnSize.Relative());

        public RowItem Item(ColumnSize size)
        {
            var item = new RowItem(size);
            _items.Add(item);
            return item;
        }

        internal override LayoutResult Layout(double width, double height, TextStyle inherited, bool force)
        {
            if (_items.Count == 0)
                return LayoutResult.Done(Fragment.Empty);

            TextStyle style = inherited.Over(LocalStyle);
            var sizes = new List<ColumnSize>(_items.Count);
            foreach (var item in _items)
                sizes.Add(item.Size);
            double[] widths = ColumnSize.Distribute(sizes, width - _gap * (_items.Count - 1));

            var fragments = new Fragment[_items.Count];
            double rowHeight = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                fragments[i] = _items[i].Layout(widths[i], double.PositiveInfinity, style, force: false).Placed ?? Fragment.Empty;
                rowHeight = Math.Max(rowHeight, fragments[i].Height);
            }

            if (rowHeight <= height + 1e-6)
                return LayoutResult.Done(Draw(fragments, widths, rowHeight));

            // Cabe en una página vacía: pasa completa a la siguiente.
            if (!force)
                return LayoutResult.DoesNotFit(this);
            return Split(widths, height, style);
        }

        // Más alta que una página: cada elemento coloca lo que quepa y el resto sigue en la misma columna de la página siguiente.
        private LayoutResult Split(double[] widths, double height, TextStyle style)
        {
            var fragments = new Fragment[_items.Count];
            var rest = new List<RowItem>(_items.Count);
            double rowHeight = 0;
            bool pending = false;

            for (int i = 0; i < _items.Count; i++)
            {
                LayoutResult result = _items[i].Layout(widths[i], height, style, force: true);
                fragments[i] = result.Placed ?? Fragment.Empty;
                rowHeight = Math.Max(rowHeight, fragments[i].Height);
                rest.Add(_items[i].Continue(result.Remaining));
                pending |= result.Remaining != null;
            }

            if (rowHeight <= 0)
                throw new LayoutException("Un elemento de la fila (Row) no avanza al dividirse entre páginas.");

            Element? remaining = pending ? new RowBlock(rest) { _gap = _gap, LocalStyle = LocalStyle } : null;
            return new LayoutResult(Draw(fragments, widths, rowHeight), remaining);
        }

        private Fragment Draw(Fragment[] fragments, double[] widths, double height)
        {
            var offsets = new double[fragments.Length];
            for (int i = 1; i < offsets.Length; i++)
                offsets[i] = offsets[i - 1] + widths[i - 1] + _gap;

            return new Fragment(height, (context, left, top) =>
            {
                for (int i = 0; i < fragments.Length; i++)
                    fragments[i].Draw(context, left + offsets[i], top);
            });
        }
    }

    public sealed class RowItem : ContainerBase<RowItem>
    {
        private Element? _child;

        internal RowItem(ColumnSize size)
        {
            Size = size;
        }

        internal ColumnSize Size { get; }

        // Mismo ancho, con lo que falta del contenido (o vacío si ya terminó). El estilo ya va en el resto.
        internal RowItem Continue(Element? remaining) => new RowItem(Size) { _child = remaining };

        internal override void AddChild(Element element)
        {
            if (_child != null)
                throw new InvalidOperationException("Cada Item() de una fila admite un solo elemento. Usa Column() para agregar varios.");
            _child = element;
        }

        internal override LayoutResult Layout(double width, double height, TextStyle style, bool force)
            => SingleChild.Layout(_child, LocalStyle, width, height, style, force);
    }
}
