using InkSharp;

// Uso: dotnet run -- [salida.pdf] [logo.jpg]   (sin logo, usa el logo.jpg del ejemplo si existe)
string output = args.Length > 0 ? args[0] : "factura.pdf";
string logoPath = args.Length > 1 ? args[1] : "logo.jpg";
PdfImage? logo = File.Exists(logoPath) ? PdfImage.FromFile(logoPath) : null;
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);

const string azul = "#1F4788";
const string suave = "#EEF3FA";

var articulos = Enumerable.Range(1, 40)
    .Select(i => (Nombre: $"Artículo {i:00} - Material de oficina", Cantidad: i % 3 + 1, Precio: 12.5m + i))
    .ToList();

Pdf.Create("Factura F001-123")
    .Author("InkSharp")
    .Page(page =>
    {
        page.Size(PageSize.A4).Margin(Units.Mm(15));
        page.DefaultTextStyle(new TextStyle { FontSize = 10 });

        page.Header().Row(row =>
        {
            if (logo != null)
                row.Item(Units.Mm(30)).Image(logo).Width(Units.Mm(28));
            row.Item().Text("FACTURA ELECTRÓNICA\nF001-123").FontSize(16).Bold().Color(azul).AlignRight();
        });

        page.Content().Column(col =>
        {
            col.Row(row =>
            {
                row.Item().Column(c =>
                {
                    c.Gap(3);
                    c.Text("CLIENTE").Bold().Color(azul);
                    c.Text("José Peña Muñoz\nAv. Señor de los Milagros 123, Lima");
                });
                row.Item().Column(c =>
                {
                    c.Gap(3);
                    c.Text("FECHA").Bold().Color(azul).AlignRight();
                    c.Text("1 de octubre de 2026").AlignRight();
                });
            });

            col.Text("Celdas combinadas y alineación").FontSize(13).Bold().Color(azul);
            col.Table(t =>
            {
                t.Columns(90, "*", "*", "*");
                t.Header(h =>
                {
                    h.Cell("Turno").RowSpan(2).AlignMiddle().AlignCenter();
                    h.Cell("Semana 1").ColumnSpan(3).AlignCenter();
                    h.Row("Lunes", "Miércoles", "Viernes");
                });

                t.Cell("Mañana").RowSpan(2).AlignMiddle().AlignCenter().Bold();
                t.Cell("Arriba izquierda");
                t.Cell("Centro\n(combina 2 filas)").RowSpan(2).AlignMiddle().AlignCenter().Background("#FFF4D6");
                t.Cell("Abajo derecha").RowSpan(2).AlignBottom().AlignRight();
                t.Cell("Segunda fila");

                t.Cell("Tarde").AlignMiddle().AlignCenter().Bold();
                t.Cell("Esta celda combina tres columnas hacia la derecha").ColumnSpan(3).AlignCenter().Background(suave);

                t.Cell("Noche").AlignMiddle().AlignCenter().Bold();
                t.Cell("Arriba").AlignTop();
                t.Cell("Medio").AlignMiddle().AlignCenter();
                t.Cell("Texto largo que ocupa varias líneas para que la fila sea más alta y se note la alineación vertical.");
            });

            col.Text("Detalle del pedido").FontSize(13).Bold().Color(azul);
            col.Table(t =>
            {
                t.Columns("*", 50, 70, 80);
                t.HeaderBackground(azul).HeaderStyle(new TextStyle { Color = PdfColor.White });
                t.Header(h =>
                {
                    h.Cell("Descripción");
                    h.Cell("Cant.").AlignCenter();
                    h.Cell("Precio").AlignRight();
                    h.Cell("Importe").AlignRight();
                });

                foreach (var a in articulos)
                {
                    t.Cell(a.Nombre);
                    t.Cell(a.Cantidad.ToString()).AlignCenter();
                    t.Cell($"S/ {a.Precio:0.00}").AlignRight();
                    t.Cell($"S/ {a.Cantidad * a.Precio:0.00}").AlignRight();
                }

                decimal total = articulos.Sum(a => a.Cantidad * a.Precio);
                t.Cell("TOTAL").ColumnSpan(3).AlignRight().Bold().Background(suave);
                t.Cell($"S/ {total:0.00}").AlignRight().Bold().Background(suave);
            });

            col.Text("Gracias por su compra. El pago debe realizarse en un plazo máximo de 30 días. Para cualquier consulta, escríbanos indicando el número de factura.")
                .Justify().Italic().Padding(10).Background(suave).KeepTogether();
        });

        page.Footer().PageNumber("Página {page} de {pages}").FontSize(9).AlignCenter().Color("#777777");
    })
    .Save(output);

Console.WriteLine($"PDF generado: {Path.GetFullPath(output)}");
