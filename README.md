# InkSharp

Genera documentos PDF desde .NET con un motor propio y **sin dependencias**. Crea facturas, reportes y cartas con tablas (incluidas celdas combinadas), texto, imágenes y paginación automática.

`netstandard2.0`: funciona en .NET Framework 4.6.1+, .NET Core y .NET 5 a 10.

## Instalación

```bash
dotnet add package InkSharp
```

## Inicio rápido

```csharp
using InkSharp;

Pdf.Create("Mi pedido")
    .Page(page =>
    {
        page.Size(PageSize.A4).Margin(40);
        page.Header().Text("Pedido de venta").FontSize(20).Bold();

        page.Content().Column(col =>
        {
            col.Text("Cliente: María López");

            col.Table(t =>
            {
                t.Columns("*", 70, 100);
                t.Header("Producto", "Cantidad", "Importe");
                t.Row("Cable HDMI", "2", "S/ 40.00");
            });

            col.Text("Total: S/ 40.00").Bold().AlignRight();
        });

        page.Footer().PageNumber("Página {page} de {pages}").AlignCenter();
    })
    .Save("pedido.pdf");
```

El texto se ajusta solo, las tablas pasan a la página siguiente repitiendo la cabecera, y el encabezado y el pie se repiten en cada página.

## Tablas y celdas combinadas

Las celdas se colocan de izquierda a derecha y saltan los espacios ya ocupados, igual que en HTML.

```csharp
col.Table(t =>
{
    t.Columns(90, "*", "*");                 // fijo en puntos, "*" o "2*" proporcional
    t.Header(h =>
    {
        h.Cell("Turno").RowSpan(2).AlignMiddle();
        h.Cell("Semana 1").ColumnSpan(2).AlignCenter();
        h.Row("Lunes", "Martes");
    });

    t.Cell("Mañana").RowSpan(2).AlignMiddle().Bold();     // combina hacia abajo
    t.Cell("Arriba a la izquierda");
    t.Cell("Abajo a la derecha").RowSpan(2).AlignBottom().AlignRight();
    t.Cell("Segunda fila");

    t.Cell("Combina dos columnas").ColumnSpan(2).Background("#EEF3FA");
    t.Cell().Image(logo).Width(40);                       // una celda también admite imágenes
});
```

| Celda | |
| --- | --- |
| `ColumnSpan(n)` / `RowSpan(n)` | combinar hacia la derecha / hacia abajo |
| `AlignLeft()` / `AlignCenter()` / `AlignRight()` | alineación horizontal |
| `AlignTop()` / `AlignMiddle()` / `AlignBottom()` | alineación vertical |
| `Background(color)`, `Padding(n)`, `Bold()`, `FontSize(n)`, `Color(c)` | estilo |

En la tabla: `Padding(n)`, `Border(color, ancho)`, `NoBorder()`, `HeaderBackground(color)`, `HeaderStyle(estilo)`.

En la tabla también: `KeepTogether()` y `KeepWithNext()`.

### Saltos de página en tablas

- Una tabla larga corta entre filas y repite la cabecera en cada página.
- Una celda combinada con `RowSpan` que cruza el salto se reparte: cada página dibuja su tramo, con los bordes cerrados, y el texto continúa en la siguiente.
- Una fila que cabe en una página nunca se parte; si no hay espacio, pasa completa a la siguiente.
- Una fila más alta que una página entera se divide por líneas.

## Texto

```csharp
col.Text("Resumen").FontSize(16).Bold().Color("#1F4788");
col.Text("Párrafo largo...").Justify().Padding(10).Background("#F0F5FA").Border("#CCCCCC");
col.Text("Firma").AlignRight().KeepTogether();
```

Estilo: `Font(FontFamily.Times)`, `FontSize`, `Bold`, `Italic`, `Color`, `LineHeight`, `AlignLeft/Center/Right`, `Justify`. Los estilos se heredan de la página, la columna, la tabla o la celda.

## Controlar los saltos de página

```csharp
col.Table(...).KeepTogether();            // si no cabe en lo que queda, pasa entera a la página siguiente
col.Text("Resumen").Bold().KeepWithNext(); // no se queda solo al final: va junto a lo que sigue
col.Column(...).KeepTogether();
```

Si el bloque es más alto que una página completa, se parte igual: nunca produce error.

## Varios documentos en un PDF

Cada `Page(...)` es una sección que empieza en una página nueva. Así se generan, por ejemplo, los reportes de 10 trabajadores en un solo archivo:

```csharp
var pdf = Pdf.Create("Reportes");
foreach (var r in reportes)
    pdf.Page(page =>
    {
        page.Content().Text(r.Nombre);
        page.Footer().PageNumber("Página {sectionPage} de {sectionPages}");  // numeración propia de cada reporte
    });
pdf.Save("reportes.pdf");
```

`{page}` y `{pages}` cuentan todo el documento; `{sectionPage}` y `{sectionPages}`, solo la sección.

## Filas, columnas e imágenes

```csharp
page.Header().Row(row =>
{
    row.Item(100).Image("logo.jpg").Width(80);     // ancho fijo
    row.Item().Text("FACTURA\nF001-123").AlignRight(); // resto del espacio
});

col.Gap(8);                  // separación entre elementos
col.Divider();               // línea horizontal
col.Space(20);               // espacio vertical
col.Image(logo).Height(60).AlignCenter();
```

Una fila (`Row`) que cabe en una página no se parte; si no hay espacio, pasa completa a la siguiente. Si es más alta que una página entera, cada elemento continúa en la página siguiente dentro de su misma columna.

Imágenes: JPEG y PNG. Una misma `PdfImage` se guarda una sola vez aunque se repita en cada página, y puede compartirse entre documentos que se generan al mismo tiempo.

## Dibujo por coordenadas

Para control total existe la API de bajo nivel: `PdfDocument`, `PdfPage.DrawText`, `DrawTextBox`, `DrawRectangle`, `DrawLine`, `DrawImage`, etc.

## Alcance

- Texto en español y alfabeto latino (WinAnsi) con las 12 fuentes estándar: Helvetica, Times y Courier.
- El encabezado y el pie se repiten completos en cada página: si no dejan espacio para el contenido, se produce un `LayoutException`.

## Ejemplos

En `samples/` hay dos programas listos para ejecutar; los PDFs quedan en `samples/salida/`:

```bash
cd samples/InkSharp.Demo && dotnet run -- ../salida/factura.pdf   # factura con logo, tabla larga y celdas combinadas
cd samples/InkSharp.Reporte && dotnet run -- ../salida            # reportes de evaluación: 1 evaluador, 2 evaluadores y 10 evaluados en un PDF
```

## Licencia

MIT
