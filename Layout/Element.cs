using System;

namespace InkSharp
{
    public abstract class Element
    {
        internal Element()
        {
        }

        // Coloca lo que quepa y devuelve el resto para la página siguiente.
        // force: el elemento está al inicio de una página vacía y debe colocar algo aunque tenga que partirse.
        internal abstract LayoutResult Layout(double width, double height, TextStyle style, bool force);

        internal bool KeepsWithNext { get; set; }
    }

    public enum VerticalAlign
    {
        Top,
        Middle,
        Bottom,
    }

    public sealed class LayoutException : Exception
    {
        public LayoutException(string message)
            : base(message)
        {
        }
    }

    internal readonly struct LayoutResult
    {
        public LayoutResult(Fragment? placed, Element? remaining)
        {
            Placed = placed;
            Remaining = remaining;
        }

        public Fragment? Placed { get; }

        public Element? Remaining { get; }

        public static LayoutResult Done(Fragment fragment) => new LayoutResult(fragment, null);

        public static LayoutResult DoesNotFit(Element element) => new LayoutResult(null, element);
    }

    internal sealed class Fragment
    {
        private readonly Action<DrawContext, double, double>? _draw;

        public Fragment(double height, Action<DrawContext, double, double>? draw)
        {
            Height = height;
            _draw = draw;
        }

        public static Fragment Empty { get; } = new Fragment(0, null);

        public double Height { get; }

        public void Draw(DrawContext context, double x, double y) => _draw?.Invoke(context, x, y);
    }

    internal sealed class DrawContext
    {
        public DrawContext(PdfPage page, int pageNumber, int totalPages, int sectionPage, int sectionPages)
        {
            Page = page;
            PageNumber = pageNumber;
            TotalPages = totalPages;
            SectionPage = sectionPage;
            SectionPages = sectionPages;
        }

        public PdfPage Page { get; }

        public int PageNumber { get; }

        public int TotalPages { get; }

        public int SectionPage { get; }

        public int SectionPages { get; }
    }

    internal readonly struct Spacing
    {
        public Spacing(double all)
            : this(all, all, all, all)
        {
        }

        public Spacing(double top, double right, double bottom, double left)
        {
            if (top < 0 || right < 0 || bottom < 0 || left < 0)
                throw new ArgumentOutOfRangeException(nameof(top), "Los márgenes y el padding no pueden ser negativos.");
            Top = top;
            Right = right;
            Bottom = bottom;
            Left = left;
        }

        public double Top { get; }

        public double Right { get; }

        public double Bottom { get; }

        public double Left { get; }
    }

    internal static class SingleChild
    {
        public static LayoutResult Layout(Element? child, TextStyle local, double width, double height, TextStyle inherited, bool force)
        {
            if (child == null)
                return LayoutResult.Done(Fragment.Empty);

            LayoutResult result = child.Layout(width, height, inherited.Over(local), force);
            return result.Remaining == null
                ? result
                : new LayoutResult(result.Placed, new StyleScope(local, result.Remaining));
        }
    }

    // Conserva el estilo del contenedor cuando su contenido continúa en otra página.
    internal sealed class StyleScope : Element
    {
        private readonly TextStyle _local;
        private readonly Element _inner;

        public StyleScope(TextStyle local, Element inner)
        {
            _local = local;
            _inner = inner;
        }

        internal override LayoutResult Layout(double width, double height, TextStyle style, bool force)
            => SingleChild.Layout(_inner, _local, width, height, style, force);
    }
}
