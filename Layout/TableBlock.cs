using System;
using System.Collections.Generic;
using System.Linq;

namespace InkSharp
{
    /// <summary>
    /// Tabla con celdas combinadas. Las celdas se colocan de izquierda a derecha y saltan
    /// los espacios ocupados por celdas con RowSpan, como en HTML.
    /// </summary>
    public sealed class TableBlock : StyledElement<TableBlock>
    {
        private readonly List<ColumnSize> _columns = new List<ColumnSize>();
        private readonly List<TableCell> _header = new List<TableCell>();
        private readonly List<TableCell> _body = new List<TableCell>();
        private TextStyle _headerStyle = new TextStyle { Bold = true };
        private PdfColor? _headerBackground = PdfColor.FromHex("#F2F2F2");
        private PdfColor? _borderColor = PdfColor.FromGray(0.6);
        private double _borderWidth = 0.5;
        private double _padding = 5;
        private bool _keepTogether;

        internal TableBlock()
        {
        }

        /// <summary>Anchos de columna: números fijos en puntos, "*" o "2*" proporcionales.</summary>
        public TableBlock Columns(params ColumnSize[] columns)
        {
            if (columns == null || columns.Length == 0)
                throw new ArgumentException("Indica al menos una columna.", nameof(columns));
            _columns.Clear();
            _columns.AddRange(columns);
            return this;
        }

        public TableBlock Header(params string[] texts)
        {
            TableRows.AddRow(_header, texts);
            return this;
        }

        public TableBlock Header(Action<TableHeader> build)
        {
            if (build == null)
                throw new ArgumentNullException(nameof(build));
            build(new TableHeader(_header));
            return this;
        }

        public TableCell Cell() => TableRows.Add(_body, new TableCell());

        public TableCell Cell(string text) => TableRows.Add(_body, TableCell.WithText(text));

        /// <summary>Agrega una fila completa de textos.</summary>
        public TableBlock Row(params string[] texts)
        {
            TableRows.AddRow(_body, texts);
            return this;
        }

        public TableBlock HeaderStyle(TextStyle style)
        {
            _headerStyle = _headerStyle.Over(style);
            return this;
        }

        public TableBlock HeaderBackground(PdfColor? color)
        {
            _headerBackground = color;
            return this;
        }

        public TableBlock HeaderBackground(string hex) => HeaderBackground(PdfColor.FromHex(hex));

        public TableBlock Padding(double padding)
        {
            if (padding < 0)
                throw new ArgumentOutOfRangeException(nameof(padding), "El padding no puede ser negativo.");
            _padding = padding;
            return this;
        }

        public TableBlock Border(PdfColor color, double width = 0.5)
        {
            _borderColor = color;
            _borderWidth = width;
            return this;
        }

        public TableBlock Border(string hex, double width = 0.5) => Border(PdfColor.FromHex(hex), width);

        /// <summary>Si la tabla no cabe en lo que queda de la página, pasa entera a la siguiente.</summary>
        public TableBlock KeepTogether()
        {
            _keepTogether = true;
            return this;
        }

        public TableBlock NoBorder()
        {
            _borderColor = null;
            return this;
        }

        internal override LayoutResult Layout(double width, double height, TextStyle style, bool force)
        {
            if (_columns.Count == 0)
                throw new LayoutException("La tabla no tiene columnas. Defínelas con Columns(...).");

            List<CellSpec> body = Assign(_body, header: false, out int rows);
            return Place(width, height, style, force, new PendingRows(new List<CellSpec>(), body, 0, 0, rows), this);
        }

        // Asigna fila y columna a cada celda, saltando los espacios ocupados por celdas combinadas.
        private List<CellSpec> Assign(List<TableCell> cells, bool header, out int rowCount)
        {
            int columnCount = _columns.Count;
            var occupied = new List<bool[]>();
            var specs = new List<CellSpec>();
            int row = 0, col = 0;

            foreach (var cell in cells)
            {
                int span = cell.ColumnSpanValue;
                if (span > columnCount)
                    throw new LayoutException($"Una celda combina {span} columnas, pero la tabla solo tiene {columnCount}.");

                if (cell.StartsRow && col > 0)
                {
                    row++;
                    col = 0;
                }

                while (true)
                {
                    if (col + span > columnCount)
                    {
                        row++;
                        col = 0;
                        continue;
                    }
                    if (IsFree(occupied, row, col, span))
                        break;
                    col++;
                }

                for (int r = row; r < row + cell.RowSpanValue; r++)
                {
                    while (occupied.Count <= r)
                        occupied.Add(new bool[columnCount]);
                    for (int c = col; c < col + span; c++)
                        occupied[r][c] = true;
                }

                specs.Add(new CellSpec(cell, row, col, cell.RowSpanValue, span, cell, header));
                col += span;
                if (cell.EndsRow)
                {
                    row++;
                    col = 0;
                }
            }

            rowCount = occupied.Count;
            return specs;
        }

        private static bool IsFree(List<bool[]> occupied, int row, int col, int span)
        {
            if (row >= occupied.Count)
                return true;
            for (int c = col; c < col + span; c++)
            {
                if (occupied[row][c])
                    return false;
            }
            return true;
        }

        private LayoutResult Place(double width, double height, TextStyle inherited, bool force, PendingRows pending, Element self)
        {
            TextStyle style = inherited.Over(LocalStyle);
            double[] x = ColumnPositions(width);
            Grid header = Measure(Assign(_header, header: true, out int headerRows), 0, headerRows, x, style.Over(_headerStyle));

            double top = header.Total;
            if (top > height + Epsilon)
            {
                if (force)
                    throw new LayoutException("La cabecera de la tabla no cabe en una página.");
                return LayoutResult.DoesNotFit(self);
            }

            var boxes = new List<CellBox>();
            foreach (var cell in header.Cells)
                boxes.Add(FullBox(header, cell, 0));

            Grid body = MeasureWindow(pending, height - top, x, style);
            int fullRows = 0;
            while (fullRows < body.RowCount && top + body.Offsets[fullRows + 1] <= height + Epsilon)
                fullRows++;

            if (_keepTogether && !force && self == this && fullRows < pending.RowCount)
                return LayoutResult.DoesNotFit(self);

            if (fullRows == pending.RowCount)
            {
                foreach (var cell in body.Cells)
                    boxes.Add(FullBox(body, cell, top));
                return LayoutResult.Done(Draw(boxes, top + body.Total));
            }

            if (fullRows > 0)
                return BreakBetweenRows(body, pending, fullRows, top, style, boxes);

            // La primera fila no cabe: se mueve a la página siguiente, salvo que ya estemos al inicio de una.
            if (!force)
                return LayoutResult.DoesNotFit(self);
            return BreakInsideRow(body, pending, height, top, style, boxes);
        }

        // Mide solo las filas que pueden caber en la página, no toda la tabla pendiente: la ventana crece al doble
        // mientras quepa y siempre incluye completas las celdas combinadas que empiezan dentro de ella.
        private Grid MeasureWindow(PendingRows pending, double available, double[] x, TextStyle style)
        {
            int end = Math.Min(pending.RowCount, 32);
            while (true)
            {
                var cells = new List<CellSpec>(pending.Carried);
                foreach (var cell in pending.Carried)
                    end = Math.Max(end, cell.RowSpan);
                for (int i = pending.Start; i < pending.Cells.Count && pending.Cells[i].Row - pending.RowBase < end; i++)
                {
                    cells.Add(pending.Cells[i]);
                    end = Math.Max(end, pending.Cells[i].Row - pending.RowBase + pending.Cells[i].RowSpan);
                }

                Grid grid = Measure(cells, pending.RowBase, end, x, style);
                if (end >= pending.RowCount || grid.Total > available + Epsilon)
                    return grid;
                end = Math.Min(pending.RowCount, end * 2);
            }
        }

        // Corta antes de la fila indicada. Las celdas combinadas que cruzan el corte se reparten entre páginas.
        // Las celdas que empiezan después del corte no se copian: la continuación sigue leyendo la lista original.
        private LayoutResult BreakBetweenRows(Grid body, PendingRows pending, int breakRow, double top, TextStyle style, List<CellBox> boxes)
        {
            int nextBase = pending.RowBase + breakRow;
            var carried = new List<CellSpec>();
            foreach (var cell in body.Cells)
            {
                int end = cell.Row + cell.Spec.RowSpan;
                if (end <= breakRow)
                {
                    boxes.Add(FullBox(body, cell, top));
                }
                else if (cell.Row < breakRow)
                {
                    double portion = body.Offsets[breakRow] - body.Offsets[cell.Row];
                    LayoutResult split = SplitContent(cell, portion, style, force: false);
                    boxes.Add(PartialBox(cell, top + body.Offsets[cell.Row], portion, split.Placed));
                    carried.Add(cell.Spec.Continue(nextBase, end - breakRow, split.Remaining));
                }
            }

            var rest = new PendingRows(carried, pending.Cells, pending.IndexOfRow(breakRow), nextBase, pending.RowCount - breakRow);
            return new LayoutResult(Draw(boxes, top + body.Offsets[breakRow]), new TableContinuation(this, rest));
        }

        // Una fila más alta que la página: su contenido se parte por líneas y continúa en la siguiente.
        private LayoutResult BreakInsideRow(Grid body, PendingRows pending, double height, double top, TextStyle style, List<CellBox> boxes)
        {
            double portion = height - top;
            if (portion <= Epsilon)
                throw new LayoutException("La cabecera de la tabla no deja espacio para sus filas.");

            int headerBoxes = boxes.Count;
            foreach (bool force in new[] { false, true })
            {
                boxes.RemoveRange(headerBoxes, boxes.Count - headerBoxes);
                var carried = new List<CellSpec>();
                bool progress = false;

                foreach (var cell in body.Cells)
                {
                    if (cell.Row > 0)
                        continue;

                    LayoutResult split = SplitContent(cell, portion, style, force);
                    if (split.Placed != null && split.Placed.Height > 0)
                        progress = true;
                    boxes.Add(PartialBox(cell, top, portion, split.Placed));
                    carried.Add(cell.Spec.Continue(pending.RowBase, cell.Spec.RowSpan, split.Remaining));
                }

                if (progress)
                {
                    var rest = new PendingRows(carried, pending.Cells, pending.IndexOfRow(1), pending.RowBase, pending.RowCount);
                    return new LayoutResult(Draw(boxes, height), new TableContinuation(this, rest));
                }
            }

            throw new LayoutException("Una celda de la tabla no cabe ni en una página completa.");
        }

        private static LayoutResult SplitContent(MeasuredCell cell, double portion, TextStyle style, bool force)
        {
            Element? content = cell.Spec.Content;
            if (content == null)
                return LayoutResult.Done(Fragment.Empty);

            double inner = portion - cell.Padding * 2;
            if (inner <= 0)
                return LayoutResult.DoesNotFit(content);
            return content.Layout(cell.InnerWidth, inner, style, force);
        }

        private double[] ColumnPositions(double width)
        {
            double[] widths = ColumnSize.Distribute(_columns, width);
            var x = new double[widths.Length + 1];
            for (int i = 0; i < widths.Length; i++)
                x[i + 1] = x[i] + widths[i];
            return x;
        }

        private Grid Measure(List<CellSpec> specs, int rowBase, int rowCount, double[] x, TextStyle style)
        {
            var cells = new List<MeasuredCell>();
            foreach (var spec in specs)
            {
                double padding = spec.Source.PaddingValue ?? _padding;
                double cellWidth = x[spec.Column + spec.ColumnSpan] - x[spec.Column];
                double innerWidth = cellWidth - padding * 2;
                if (innerWidth <= 0)
                    throw new LayoutException("Una celda es más angosta que su padding.");

                if (spec.MeasuredWidth != innerWidth)
                {
                    spec.Measured = spec.Content == null
                        ? Fragment.Empty
                        : spec.Content.Layout(innerWidth, double.PositiveInfinity, style, force: false).Placed ?? Fragment.Empty;
                    spec.MeasuredWidth = innerWidth;
                }

                cells.Add(new MeasuredCell(spec, spec.Row - rowBase, x[spec.Column], cellWidth, innerWidth, padding, spec.Measured!));
            }

            return new Grid(cells, rowCount);
        }

        private CellBox FullBox(Grid grid, MeasuredCell cell, double top)
        {
            double y = top + grid.Offsets[cell.Row];
            double height = grid.Offsets[cell.Row + cell.Spec.RowSpan] - grid.Offsets[cell.Row];
            return PartialBox(cell, y, height, cell.Content);
        }

        private CellBox PartialBox(MeasuredCell cell, double y, double height, Fragment? content)
        {
            PdfColor? background = cell.Spec.Source.BackgroundValue ?? (cell.Spec.IsHeader ? _headerBackground : null);
            return new CellBox(cell.X, y, cell.Width, height, cell.Padding, background, content, cell.Spec.Source.VerticalAlignment);
        }

        private Fragment Draw(List<CellBox> boxes, double height)
        {
            PdfColor? borderColor = _borderColor;
            double borderWidth = _borderWidth;

            return new Fragment(height, (context, x, y) =>
            {
                PdfPage page = context.Page;

                // Primero fondos, luego contenido y al final bordes, para que nada tape las líneas.
                foreach (var box in boxes)
                {
                    if (box.Background != null)
                        page.DrawRectangle(x + box.X, y + box.Y, box.Width, box.Height, fill: box.Background);
                }

                foreach (var box in boxes)
                {
                    if (box.Content == null)
                        continue;
                    double free = box.Height - box.Padding * 2 - box.Content.Height;
                    double offset = box.Align switch
                    {
                        VerticalAlign.Middle => free / 2,
                        VerticalAlign.Bottom => free,
                        _ => 0,
                    };
                    box.Content.Draw(context, x + box.X + box.Padding, y + box.Y + box.Padding + Math.Max(0, offset));
                }

                if (borderColor != null)
                    foreach (var box in boxes)
                        page.DrawRectangle(x + box.X, y + box.Y, box.Width, box.Height, stroke: borderColor, strokeWidth: borderWidth);
            });
        }

        private const double Epsilon = 1e-6;

        private sealed class TableContinuation : Element
        {
            private readonly TableBlock _table;
            private readonly PendingRows _pending;

            public TableContinuation(TableBlock table, PendingRows pending)
            {
                _table = table;
                _pending = pending;
            }

            internal override LayoutResult Layout(double width, double height, TextStyle style, bool force)
                => _table.Place(width, height, style, force, _pending, this);
        }

        // Filas del cuerpo que faltan: las celdas partidas que continúan (Carried, en la fila RowBase)
        // y las celdas originales desde Start. Las filas se cuentan a partir de RowBase.
        private sealed class PendingRows
        {
            public PendingRows(List<CellSpec> carried, List<CellSpec> cells, int start, int rowBase, int rowCount)
            {
                Carried = carried;
                Cells = cells;
                Start = start;
                RowBase = rowBase;
                RowCount = rowCount;
            }

            public List<CellSpec> Carried { get; }
            public List<CellSpec> Cells { get; }
            public int Start { get; }
            public int RowBase { get; }
            public int RowCount { get; }

            // Las celdas están ordenadas por fila: primer índice desde Start cuya fila relativa es >= row.
            public int IndexOfRow(int row)
            {
                int i = Start;
                while (i < Cells.Count && Cells[i].Row - RowBase < row)
                    i++;
                return i;
            }
        }

        // Celda ubicada en la cuadrícula. Content es lo que falta dibujar (null si ya se dibujó todo).
        private sealed class CellSpec
        {
            public CellSpec(TableCell source, int row, int column, int rowSpan, int columnSpan, Element? content, bool isHeader)
            {
                Source = source;
                Row = row;
                Column = column;
                RowSpan = rowSpan;
                ColumnSpan = columnSpan;
                Content = content;
                IsHeader = isHeader;
            }

            public TableCell Source { get; }
            public int Row { get; }
            public int Column { get; }
            public int RowSpan { get; }
            public int ColumnSpan { get; }
            public Element? Content { get; }
            public bool IsHeader { get; }
            public Fragment? Measured { get; set; }
            public double MeasuredWidth { get; set; } = -1;

            public CellSpec Continue(int row, int rowSpan, Element? content) => new CellSpec(Source, row, Column, rowSpan, ColumnSpan, content, IsHeader);
        }

        private sealed class MeasuredCell
        {
            public MeasuredCell(CellSpec spec, int row, double x, double width, double innerWidth, double padding, Fragment content)
            {
                Spec = spec;
                Row = row;
                X = x;
                Width = width;
                InnerWidth = innerWidth;
                Padding = padding;
                Content = content;
            }

            public CellSpec Spec { get; }
            public int Row { get; }
            public double X { get; }
            public double Width { get; }
            public double InnerWidth { get; }
            public double Padding { get; }
            public Fragment Content { get; }
            public double RequiredHeight => Content.Height + Padding * 2;
        }

        private sealed class CellBox
        {
            public CellBox(double x, double y, double width, double height, double padding, PdfColor? background, Fragment? content, VerticalAlign align)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
                Padding = padding;
                Background = background;
                Content = content;
                Align = align;
            }

            public double X { get; }
            public double Y { get; }
            public double Width { get; }
            public double Height { get; }
            public double Padding { get; }
            public PdfColor? Background { get; }
            public Fragment? Content { get; }
            public VerticalAlign Align { get; }
        }

        private sealed class Grid
        {
            public Grid(List<MeasuredCell> cells, int rowCount)
            {
                Cells = cells;
                RowCount = rowCount;

                var heights = new double[rowCount];
                foreach (var cell in cells.Where(c => c.Spec.RowSpan == 1))
                    heights[cell.Row] = Math.Max(heights[cell.Row], cell.RequiredHeight);

                // Si una celda combinada necesita más alto, se reparte entre las filas que ocupa.
                foreach (var cell in cells.Where(c => c.Spec.RowSpan > 1).OrderBy(c => c.Spec.RowSpan))
                {
                    double current = 0;
                    for (int r = cell.Row; r < cell.Row + cell.Spec.RowSpan; r++)
                        current += heights[r];
                    if (cell.RequiredHeight > current)
                    {
                        double extra = (cell.RequiredHeight - current) / cell.Spec.RowSpan;
                        for (int r = cell.Row; r < cell.Row + cell.Spec.RowSpan; r++)
                            heights[r] += extra;
                    }
                }

                Offsets = new double[rowCount + 1];
                for (int r = 0; r < rowCount; r++)
                    Offsets[r + 1] = Offsets[r] + heights[r];
            }

            public List<MeasuredCell> Cells { get; }

            public int RowCount { get; }

            public double[] Offsets { get; }

            public double Total => Offsets[RowCount];
        }
    }

    public sealed class TableHeader
    {
        private readonly List<TableCell> _cells;

        internal TableHeader(List<TableCell> cells)
        {
            _cells = cells;
        }

        public TableCell Cell() => TableRows.Add(_cells, new TableCell());

        public TableCell Cell(string text) => TableRows.Add(_cells, TableCell.WithText(text));

        public TableHeader Row(params string[] texts)
        {
            TableRows.AddRow(_cells, texts);
            return this;
        }
    }

    public sealed class TableCell : ContainerBase<TableCell>
    {
        private Element? _child;

        internal TableCell()
        {
        }

        internal int ColumnSpanValue { get; private set; } = 1;
        internal int RowSpanValue { get; private set; } = 1;
        internal VerticalAlign VerticalAlignment { get; private set; } = VerticalAlign.Top;
        internal PdfColor? BackgroundValue { get; private set; }
        internal double? PaddingValue { get; private set; }
        internal bool StartsRow { get; set; }
        internal bool EndsRow { get; set; }

        /// <summary>Combina la celda con las columnas de su derecha.</summary>
        public TableCell ColumnSpan(int columns)
        {
            if (columns < 1)
                throw new ArgumentOutOfRangeException(nameof(columns), "Debe ser al menos 1.");
            ColumnSpanValue = columns;
            return this;
        }

        /// <summary>Combina la celda con las filas de abajo.</summary>
        public TableCell RowSpan(int rows)
        {
            if (rows < 1)
                throw new ArgumentOutOfRangeException(nameof(rows), "Debe ser al menos 1.");
            RowSpanValue = rows;
            return this;
        }

        public TableCell AlignTop() => AlignVertical(VerticalAlign.Top);

        public TableCell AlignMiddle() => AlignVertical(VerticalAlign.Middle);

        public TableCell AlignBottom() => AlignVertical(VerticalAlign.Bottom);

        public TableCell Background(PdfColor color)
        {
            BackgroundValue = color;
            return this;
        }

        public TableCell Background(string hex) => Background(PdfColor.FromHex(hex));

        public TableCell Padding(double padding)
        {
            if (padding < 0)
                throw new ArgumentOutOfRangeException(nameof(padding), "El padding no puede ser negativo.");
            PaddingValue = padding;
            return this;
        }

        internal static TableCell WithText(string text)
        {
            var cell = new TableCell();
            cell.Text(text ?? "");
            return cell;
        }

        internal override void AddChild(Element element)
        {
            if (_child != null)
                throw new InvalidOperationException("Una celda admite un solo elemento. Usa Column() para agregar varios.");
            _child = element;
        }

        internal override LayoutResult Layout(double width, double height, TextStyle style, bool force)
            => SingleChild.Layout(_child, LocalStyle, width, height, style, force);

        private TableCell AlignVertical(VerticalAlign align)
        {
            VerticalAlignment = align;
            return this;
        }
    }

    internal static class TableRows
    {
        public static TableCell Add(List<TableCell> cells, TableCell cell)
        {
            cells.Add(cell);
            return cell;
        }

        public static void AddRow(List<TableCell> cells, string[] texts)
        {
            if (texts == null || texts.Length == 0)
                throw new ArgumentException("La fila debe tener al menos una celda.", nameof(texts));

            for (int i = 0; i < texts.Length; i++)
            {
                TableCell cell = TableCell.WithText(texts[i]);
                cell.StartsRow = i == 0;
                cell.EndsRow = i == texts.Length - 1;
                cells.Add(cell);
            }
        }
    }
}
