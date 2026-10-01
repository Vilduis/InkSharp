using System;
using System.Collections.Generic;

namespace InkSharp
{
    /// <summary>Apila elementos de arriba hacia abajo y los reparte entre páginas.</summary>
    public sealed class ColumnBlock : ContainerBase<ColumnBlock>
    {
        private readonly List<Element> _items;
        private double _gap = 12;
        private bool _keepTogether;

        // En una continuación (página siguiente) la lista original no se copia: se sigue desde _start,
        // precedida por _head, lo que quedó pendiente del elemento que se partió.
        private readonly bool _continuation;
        private readonly Element? _head;
        private readonly int _start;

        internal ColumnBlock()
            : this(new List<Element>(), null, 0, continuation: false)
        {
        }

        private ColumnBlock(List<Element> items, Element? head, int start, bool continuation)
        {
            _items = items;
            _head = head;
            _start = start;
            _continuation = continuation;
        }

        /// <summary>Separación entre elementos (12 pt por defecto).</summary>
        public ColumnBlock Gap(double gap)
        {
            if (gap < 0)
                throw new ArgumentOutOfRangeException(nameof(gap), "La separación no puede ser negativa.");
            _gap = gap;
            return this;
        }

        /// <summary>Espacio vertical adicional. Se omite al comienzo de una página.</summary>
        public ColumnBlock Space(double height)
        {
            _items.Add(new SpaceBlock(height));
            return this;
        }

        /// <summary>Si la columna no cabe en lo que queda de la página, pasa entera a la siguiente.</summary>
        public ColumnBlock KeepTogether()
        {
            _keepTogether = true;
            return this;
        }

        internal override void AddChild(Element element) => _items.Add(element);

        internal override LayoutResult Layout(double width, double height, TextStyle inherited, bool force)
        {
            LayoutResult result = LayoutItems(width, height, inherited, force);
            if (_keepTogether && !force && result.Remaining != null)
                return LayoutResult.DoesNotFit(this);
            return result;
        }

        private LayoutResult LayoutItems(double width, double height, TextStyle inherited, bool force)
        {
            TextStyle style = inherited.Over(LocalStyle);
            var placed = new List<PlacedItem>();
            int count = Count;

            for (int i = 0; i < count; i++)
            {
                Element item = ItemAt(i);
                bool first = placed.Count == 0;
                if (first && _continuation && item is SpaceBlock)
                    continue;

                double used = Used(placed);
                double offset = first ? 0 : _gap;
                double available = height - used - offset;
                LayoutResult result = available < 0
                    ? LayoutResult.DoesNotFit(item)
                    : item.Layout(width, available, style, force && first);

                if (result.Placed != null)
                    placed.Add(new PlacedItem(i, used + offset, result.Placed));

                if (result.Remaining == null)
                    continue;

                int resumeAt = i;
                if (result.Placed == null && !(item is SpaceBlock))
                {
                    // KeepWithNext: los elementos anteriores que deben ir con este también pasan a la página siguiente.
                    int keep = placed.Count;
                    while (keep > 0 && ItemAt(placed[keep - 1].Index).KeepsWithNext)
                        keep--;
                    if (keep < placed.Count && (keep > 0 || !force))
                    {
                        resumeAt = placed[keep].Index;
                        placed.RemoveRange(keep, placed.Count - keep);
                    }
                }

                if (placed.Count == 0)
                    return LayoutResult.DoesNotFit(this);

                ColumnBlock? rest = resumeAt < i
                    ? From(resumeAt)
                    : Continue(item is SpaceBlock ? null : result.Remaining, SourceIndex(i + 1));
                return new LayoutResult(Stack(placed), rest);
            }

            return LayoutResult.Done(Stack(placed));
        }

        private int Count => (_head != null ? 1 : 0) + _items.Count - _start;

        private Element ItemAt(int index) => _head == null ? _items[_start + index] : index == 0 ? _head : _items[_start + index - 1];

        private int SourceIndex(int index) => _head == null ? _start + index : _start + index - 1;

        // Lo que falta a partir del elemento indicado, completo.
        private ColumnBlock? From(int index)
            => index == 0 && _head != null ? Continue(_head, _start) : Continue(null, SourceIndex(index));

        private ColumnBlock? Continue(Element? head, int start)
        {
            if (head == null && start >= _items.Count)
                return null;
            return new ColumnBlock(_items, head, start, continuation: true) { _gap = _gap, LocalStyle = LocalStyle };
        }

        private static double Used(List<PlacedItem> placed)
            => placed.Count == 0 ? 0 : placed[placed.Count - 1].Y + placed[placed.Count - 1].Fragment.Height;

        private static Fragment Stack(List<PlacedItem> placed)
        {
            return new Fragment(Used(placed), (context, x, y) =>
            {
                foreach (var item in placed)
                    item.Fragment.Draw(context, x, y + item.Y);
            });
        }

        private readonly struct PlacedItem
        {
            public PlacedItem(int index, double y, Fragment fragment)
            {
                Index = index;
                Y = y;
                Fragment = fragment;
            }

            public int Index { get; }

            public double Y { get; }

            public Fragment Fragment { get; }
        }
    }

    internal sealed class SpaceBlock : Element
    {
        private readonly double _height;

        public SpaceBlock(double height)
        {
            if (height < 0)
                throw new ArgumentOutOfRangeException(nameof(height), "El espacio no puede ser negativo.");
            _height = height;
        }

        internal override LayoutResult Layout(double width, double height, TextStyle style, bool force)
        {
            if (_height <= height)
                return LayoutResult.Done(new Fragment(_height, null));
            return force ? LayoutResult.Done(new Fragment(Math.Max(0, height), null)) : LayoutResult.DoesNotFit(this);
        }
    }
}
