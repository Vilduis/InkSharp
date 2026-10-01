using InkSharp;

// Uso: dotnet run -- [carpeta-de-salida]
string carpeta = args.Length > 0 ? args[0] : ".";
Directory.CreateDirectory(carpeta);

var evaluadora = new Persona("E-00451", "Ramírez Gutiérrez, Ana Lucía", "Jefa de Operaciones");
var gerente = new Persona("E-00102", "Salazar Quispe, Marco Antonio", "Gerente de Operaciones");

// 1. Un evaluado con un evaluador.
Guardar("reporte.pdf", Datos.Reporte(Datos.Evaluados[0], (evaluadora, 5)));

// 2. Un evaluado con dos evaluadores; cada uno asigna sus propios objetivos.
Guardar("reporte-dos-evaluadores.pdf", Datos.Reporte(Datos.Evaluados[1], (evaluadora, 4), (gerente, 7)));

// 3. Un evaluador con 10 evaluados en un solo PDF; los objetivos varían por evaluado (hasta 32 para forzar el salto de página).
int[] cantidades = { 3, 6, 2, 9, 4, 32, 5, 7, 1, 8 };
var lote = Datos.Evaluados.Take(10).Select((e, i) => Datos.Reporte(e, (evaluadora, cantidades[i]))).ToList();
Guardar("reportes-evaluador-10-evaluados.pdf", lote.ToArray());

void Guardar(string archivo, params ReporteEvaluacion[] reportes)
{
    string ruta = Path.Combine(carpeta, archivo);
    ReportePdf.Generar(reportes).Save(ruta);
    Console.WriteLine($"PDF generado: {Path.GetFullPath(ruta)}");
}

static class ReportePdf
{
    private static readonly PdfColor Gris = PdfColor.FromHex("#E6E6E6");
    private static readonly PdfColor Borde = PdfColor.FromGray(0.3);

    // Cada reporte es una sección: empieza en página nueva y numera sus propias páginas.
    public static Pdf Generar(IEnumerable<ReporteEvaluacion> reportes)
    {
        var pdf = Pdf.Create("Evaluación de objetivos").Author("Sistema de Evaluación");
        foreach (var r in reportes)
            pdf.Page(page => Reporte(page, r));
        return pdf;
    }

    private static void Reporte(PageBuilder page, ReporteEvaluacion r)
    {
        page.Size(PageSize.A4).Margin(Units.Mm(15));
        page.DefaultTextStyle(new TextStyle { FontSize = 9.5 });

        page.Content().Column(col =>
        {
            col.Gap(14);

            col.Column(bloque =>
            {
                bloque.Gap(4);
                bloque.Table(t => DatosGenerales(t, r));
                bloque.Text("*Información registrada solo para el tipo de reporte “Cierre por cese”").FontSize(8.5).Italic();
            });

            col.Column(datos =>
            {
                datos.Gap(6);
                datos.Text("DATOS DE LA EVALUACIÓN:").Bold();
                datos.Text("El presente reporte registra el inventario de objetivos asignados al trabajador, el resultado esperado, "
                    + "resultado obtenido y porcentaje de cumplimiento de objetivos al cierre del periodo de evaluación, o por "
                    + "cierre excepcional de objetivos con motivo de desplazamiento o cese.").Justify();
            });

            for (int i = 0; i < r.Evaluaciones.Count; i++)
                col.Column(c => Evaluacion(c, r.Evaluaciones[i], i + 1, r.Evaluaciones.Count));
        });

        page.Footer().Row(row =>
        {
            row.Item().Text($"{r.Evaluado.Codigo} · {r.Evaluado.Nombre}").FontSize(8).Color("#777777");
            row.Item(90).PageNumber("Página {sectionPage} de {sectionPages}").FontSize(8).Color("#777777").AlignRight();
        });
    }

    private static void DatosGenerales(TableBlock t, ReporteEvaluacion r)
    {
        t.Columns("*", "*", "*", "*").Border(Borde);

        Seccion(t, "EVALUACIÓN DE OBJETIVOS – REPORTE INDIVIDUAL");
        Etiqueta(t, "PERIODO"); t.Cell(r.Periodo);
        Etiqueta(t, "RANGO DE PERIODO"); t.Cell(r.RangoPeriodo);
        Etiqueta(t, "GERENCIA"); t.Cell(r.Gerencia);
        Etiqueta(t, "OFICINA"); t.Cell(r.Oficina);

        Seccion(t, "EVALUADO");
        DatosPersona(t, r.Evaluado);
        Etiqueta(t, "ESTADO"); t.Cell(r.Estado).ColumnSpan(3);

        Etiqueta(t, "TIPO DE REPORTE"); t.Cell(r.TipoReporte).AlignMiddle();
        Etiqueta(t, "FECHA DE EMISIÓN DEL REPORTE"); t.Cell(r.FechaEmision).AlignMiddle();

        Seccion(t, "*MOTIVO DE CESE");
        t.Cell(r.MotivoCese ?? "—").ColumnSpan(4);
    }

    // Tres tablas seguidas: evaluador, objetivos y comentario. Los objetivos van en su propia
    // tabla para que su cabecera se repita; el evaluador no se separa del inicio de sus objetivos.
    private static void Evaluacion(ColumnBlock c, Evaluacion e, int numero, int total)
    {
        c.Gap(0);

        c.Table(t =>
        {
            t.Columns("*", "*", "*", "*").Border(Borde);
            Seccion(t, total > 1 ? $"EVALUACIÓN DE OBJETIVOS ({numero} de {total})" : "EVALUACIÓN DE OBJETIVOS");
            Seccion(t, "EVALUADOR");
            DatosPersona(t, e.Evaluador);
            Seccion(t, "OBJETIVOS ASIGNADOS Y EVALUACIÓN");
        }).KeepTogether().KeepWithNext();

        c.Table(t =>
        {
            t.Columns("2*", "*", "*", "*").Border(Borde).HeaderBackground(Gris);
            t.Header(h =>
            {
                h.Cell("Objetivos").AlignMiddle();
                h.Cell("Resultado esperado").AlignCenter().AlignMiddle();
                h.Cell("Resultado obtenido").AlignCenter().AlignMiddle();
                h.Cell("Cumplimiento de objetivos").AlignCenter().AlignMiddle();
            });

            for (int i = 0; i < e.Objetivos.Count; i++)
            {
                var o = e.Objetivos[i];
                t.Cell($"{i + 1}. {o.Descripcion}");
                t.Cell(o.Esperado).AlignCenter().AlignMiddle();
                t.Cell(o.Obtenido).AlignCenter().AlignMiddle();

                // Una sola celda que combina todas las filas de objetivos.
                if (i == 0)
                    t.Cell(e.Cumplimiento).RowSpan(e.Objetivos.Count).AlignCenter().AlignMiddle().FontSize(16).Bold();
            }
        });

        c.Table(t =>
        {
            t.Columns("*").Border(Borde);
            Seccion(t, "COMENTARIO EVALUADOR", columnas: 1);
            t.Cell(e.Comentario).Justify();
        }).KeepTogether();
    }

    private static void Seccion(TableBlock t, string titulo, int columnas = 4)
        => t.Cell(titulo).ColumnSpan(columnas).Bold().Background(Gris);

    private static void Etiqueta(TableBlock t, string texto)
        => t.Cell(texto).Bold().AlignMiddle();

    private static void DatosPersona(TableBlock t, Persona p)
    {
        Etiqueta(t, "CODIGO"); t.Cell(p.Codigo).ColumnSpan(3);
        Etiqueta(t, "APELLIDO Y NOMBRE"); t.Cell(p.Nombre).ColumnSpan(3);
        Etiqueta(t, "CARGO"); t.Cell(p.Cargo).ColumnSpan(3);
    }
}

record Persona(string Codigo, string Nombre, string Cargo);

record Objetivo(string Descripcion, string Esperado, string Obtenido);

record Evaluacion(Persona Evaluador, IReadOnlyList<Objetivo> Objetivos, string Cumplimiento, string Comentario);

record ReporteEvaluacion(
    string Periodo,
    string RangoPeriodo,
    string Gerencia,
    string Oficina,
    Persona Evaluado,
    string Estado,
    string TipoReporte,
    string FechaEmision,
    string? MotivoCese,
    IReadOnlyList<Evaluacion> Evaluaciones);

// Datos ficticios para los ejemplos.
static class Datos
{
    public static readonly Persona[] Evaluados =
    {
        new("T-01287", "Peña Muñoz, José Antonio", "Analista de Procesos Senior"),
        new("T-01302", "Castillo Rojas, María Fernanda", "Coordinadora de Almacén"),
        new("T-01315", "Huamán Torres, Luis Alberto", "Asistente de Despacho"),
        new("T-01328", "Vargas Céspedes, Rosa Elena", "Analista de Inventarios"),
        new("T-01341", "Flores Ñahui, Carlos Daniel", "Supervisor de Turno"),
        new("T-01354", "Mendoza Ríos, Lucía Beatriz", "Analista de Calidad"),
        new("T-01367", "Chávez Lozano, Jorge Iván", "Técnico de Mantenimiento"),
        new("T-01380", "Paredes Soto, Ana María", "Asistente Administrativa"),
        new("T-01393", "Gutiérrez León, Pedro Pablo", "Operador Logístico"),
        new("T-01406", "Sánchez Arce, Daniela Sofía", "Analista de Compras"),
    };

    private static readonly (string Descripcion, string Esperado, string Obtenido)[] Objetivos =
    {
        ("Reducir el tiempo promedio de atención de solicitudes internas.", "15 % menos", "18 % menos"),
        ("Documentar y publicar los procedimientos del área de despacho.", "12 procedimientos", "10 procedimientos"),
        ("Implementar el tablero de indicadores semanales de productividad.", "100 %", "100 %"),
        ("Capacitar al personal nuevo en el uso del sistema de inventarios.", "20 personas", "17 personas"),
        ("Disminuir las incidencias por errores de registro en almacén.", "Menos de 5 al mes", "4 al mes"),
        ("Mantener la exactitud del inventario cíclico mensual.", "98 %", "97.4 %"),
        ("Cumplir el plan anual de mantenimiento preventivo de equipos.", "100 %", "92 %"),
        ("Reducir las devoluciones de clientes por despachos incompletos.", "10 % menos", "12 % menos"),
        ("Atender los requerimientos de auditoría dentro del plazo establecido.", "100 %", "100 %"),
        ("Optimizar la distribución del almacén para reducir recorridos.", "1 propuesta aprobada", "1 propuesta aprobada"),
    };

    public static ReporteEvaluacion Reporte(Persona evaluado, params (Persona Evaluador, int Objetivos)[] evaluaciones)
    {
        bool cese = evaluado.Codigo.EndsWith("7");
        return new ReporteEvaluacion(
            Periodo: "2026",
            RangoPeriodo: "01/01/2026 - 31/12/2026",
            Gerencia: "Gerencia de Operaciones",
            Oficina: "Oficina Principal - Lima",
            Evaluado: evaluado,
            Estado: cese ? "Cesado" : "Activo",
            TipoReporte: cese ? "Cierre por cese" : "Cierre de periodo",
            FechaEmision: "01/10/2026",
            MotivoCese: cese ? "Renuncia voluntaria presentada el 15/09/2026, con último día de labores el 30/09/2026." : null,
            Evaluaciones: evaluaciones.Select((e, i) => Evaluacion(evaluado, e.Evaluador, e.Objetivos, i)).ToList());
    }

    private static Evaluacion Evaluacion(Persona evaluado, Persona evaluador, int cantidad, int semilla)
    {
        var objetivos = Enumerable.Range(0, cantidad)
            .Select(i => Objetivos[(i + semilla * 3) % Objetivos.Length])
            .Select(o => new Objetivo(o.Descripcion, o.Esperado, o.Obtenido))
            .ToList();

        string nombre = evaluado.Nombre.Split(", ")[1].Split(' ')[0];
        double cumplimiento = 78 + evaluado.Codigo.Sum(ch => ch) % 22 + semilla * 1.5;
        return new Evaluacion(
            evaluador,
            objetivos,
            $"{Math.Min(cumplimiento, 100):0.0} %".Replace(',', '.'),
            $"{nombre} cumplió la mayoría de los objetivos asignados por {evaluador.Nombre.Split(", ")[1]}. "
                + "Los objetivos pendientes quedaron documentados y fueron coordinados con el área para el siguiente periodo.");
    }
}
