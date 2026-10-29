using Cevicheria.Api.DTOs;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Cevicheria.Api.Services;

/// <summary>Genera los archivos Excel (.xlsx) y PDF de ventas y de cierre de caja.</summary>
public static class ExportService
{
    private const string FormatoSoles = "\"S/\" #,##0.00";
    private const string Celeste = "#2B9BD6";
    private static string Soles(decimal v) => $"S/ {v:N2}";
    private static string Local(DateTime utc) => (utc + ReporteService.OffsetLima).ToString("dd/MM/yyyy hh:mm tt");

    // ---------- Excel ----------
    public static byte[] VentasExcel(VentasReporteDto r)
    {
        using var wb = new XLWorkbook();

        var ws = wb.Worksheets.Add("Resumen");
        ws.Cell(1, 1).Value = "Reporte de ventas";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 16;
        ws.Cell(2, 1).Value = $"Del {r.Desde:dd/MM/yyyy} al {r.Hasta:dd/MM/yyyy}";
        ws.Cell(4, 1).Value = "Total cobrado"; ws.Cell(4, 2).Value = r.TotalCobrado;
        ws.Cell(5, 1).Value = "Pedidos atendidos"; ws.Cell(5, 2).Value = r.PedidosAtendidos;
        ws.Cell(6, 1).Value = "Ticket promedio"; ws.Cell(6, 2).Value = r.TicketPromedio;
        ws.Range("B4").Style.NumberFormat.Format = FormatoSoles;
        ws.Range("B6").Style.NumberFormat.Format = FormatoSoles;
        ws.Range("A4:A6").Style.Font.Bold = true;
        ws.Columns().AdjustToContents();

        Hoja(wb, "Por día", r.PorDia.Select(x => new { Fecha = x.Fecha.ToString("dd/MM/yyyy"), Pedidos = x.Pedidos, Total = x.Total }));
        Hoja(wb, "Por método", r.PorMetodo.Select(x => new { Método = x.Metodo, Pedidos = x.Pedidos, Total = x.Total }));
        Hoja(wb, "Por tipo", r.PorTipo.Select(x => new { Tipo = x.Tipo, Pedidos = x.Pedidos, Total = x.Total }));
        Hoja(wb, "Platos más vendidos", r.TopPlatos.Select(x => new { Plato = x.Plato, Cantidad = x.Cantidad, Total = x.Total }));

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void Hoja<T>(XLWorkbook wb, string nombre, IEnumerable<T> filas)
    {
        var ws = wb.Worksheets.Add(nombre);
        var lista = filas.ToList();
        if (lista.Count == 0) { ws.Cell(1, 1).Value = "Sin datos en el período."; return; }

        var tabla = ws.Cell(1, 1).InsertTable(lista, false);
        tabla.Theme = XLTableTheme.TableStyleLight9;
        // Formato de moneda en la columna "Total" (siempre la última).
        var ultima = tabla.ColumnCount();
        ws.Range(2, ultima, lista.Count + 1, ultima).Style.NumberFormat.Format = FormatoSoles;
        ws.Columns().AdjustToContents();
    }

    // ---------- PDF ----------
    public static byte[] VentasPdf(VentasReporteDto r) => Document.Create(doc => doc.Page(p =>
    {
        Pagina(p, "Reporte de ventas", $"Del {r.Desde:dd/MM/yyyy} al {r.Hasta:dd/MM/yyyy}");
        p.Content().Column(c =>
        {
            c.Spacing(14);
            c.Item().Row(row =>
            {
                Kpi(row, "Total cobrado", Soles(r.TotalCobrado));
                Kpi(row, "Pedidos", r.PedidosAtendidos.ToString());
                Kpi(row, "Ticket promedio", Soles(r.TicketPromedio));
            });
            Tabla(c, "Por método de pago", ["Método", "Pedidos", "Total"],
                r.PorMetodo.Select(x => new[] { x.Metodo, x.Pedidos.ToString(), Soles(x.Total) }));
            Tabla(c, "Por tipo de pedido", ["Tipo", "Pedidos", "Total"],
                r.PorTipo.Select(x => new[] { x.Tipo, x.Pedidos.ToString(), Soles(x.Total) }));
            Tabla(c, "Ventas por día", ["Fecha", "Pedidos", "Total"],
                r.PorDia.Select(x => new[] { x.Fecha.ToString("dd/MM/yyyy"), x.Pedidos.ToString(), Soles(x.Total) }));
            Tabla(c, "Platos más vendidos", ["Plato", "Cantidad", "Total"],
                r.TopPlatos.Select(x => new[] { x.Plato, x.Cantidad.ToString(), Soles(x.Total) }));
        });
    })).GeneratePdf();

    public static byte[] CajaPdf(ResumenCajaDto r) => Document.Create(doc => doc.Page(p =>
    {
        Pagina(p, $"Cierre de caja #{r.Id}", $"{r.Estado} · Cajero: {r.NombreUsuario}");
        p.Content().Column(c =>
        {
            c.Spacing(14);
            c.Item().Text($"Apertura: {Local(r.FechaApertura)}" +
                          (r.FechaCierre is { } f ? $"    Cierre: {Local(f)}" : string.Empty));

            Tabla(c, "Ventas del turno", ["Concepto", "Monto"],
            [
                ["Efectivo", Soles(r.TotalEfectivo)],
                ["Yape", Soles(r.TotalYape)],
                ["Plin", Soles(r.TotalPlin)],
                ["Tarjeta", Soles(r.TotalTarjeta)],
                [$"TOTAL COBRADO ({r.PedidosAtendidos} pedidos)", Soles(r.TotalCobrado)]
            ]);

            Tabla(c, "Efectivo en caja", ["Concepto", "Monto"],
            [
                ["Sencillo inicial", Soles(r.MontoInicial)],
                ["+ Cobros en efectivo", Soles(r.TotalEfectivo)],
                ["+ Otros ingresos", Soles(r.Ingresos)],
                ["- Egresos", Soles(r.Egresos)],
                ["ESPERADO EN CAJA", Soles(r.MontoEsperado)],
                ["Contado al cierre", r.MontoContado is { } mc ? Soles(mc) : "—"],
                ["DIFERENCIA", r.Diferencia is { } d ? $"{(d > 0 ? "+" : string.Empty)}{Soles(d)}" + (d < 0 ? " (faltante)" : d > 0 ? " (sobrante)" : " (cuadra)") : "—"]
            ]);

            if (r.Movimientos.Count > 0)
                Tabla(c, "Movimientos de efectivo", ["Hora", "Tipo", "Concepto", "Monto"],
                    r.Movimientos.Select(m => new[] { Local(m.Fecha), m.Tipo, m.Concepto, Soles(m.Monto) }));

            if (!string.IsNullOrWhiteSpace(r.Notas)) c.Item().Text($"Notas: {r.Notas}").Italic();
        });
    })).GeneratePdf();

    // ---------- Helpers de PDF ----------
    private static void Pagina(PageDescriptor p, string titulo, string subtitulo)
    {
        p.Size(PageSizes.A4);
        p.Margin(32);
        p.DefaultTextStyle(t => t.FontSize(11));
        p.Header().Column(h =>
        {
            h.Item().Text("Cevichería").FontSize(11).FontColor(Celeste).Bold();
            h.Item().Text(titulo).FontSize(20).Bold();
            h.Item().Text(subtitulo).FontColor(Colors.Grey.Darken1);
            h.Item().PaddingTop(6).LineHorizontal(1).LineColor(Celeste);
        });
        p.Footer().AlignCenter().Text(x =>
        {
            x.DefaultTextStyle(t => t.FontSize(9).FontColor(Colors.Grey.Medium));
            x.Span($"Generado {Local(DateTime.UtcNow)} · Página ");
            x.CurrentPageNumber();
        });
    }

    private static void Kpi(RowDescriptor row, string etiqueta, string valor) =>
        row.RelativeItem().Background("#E6F5FD").Padding(10).Column(c =>
        {
            c.Item().Text(etiqueta).FontSize(9).FontColor(Colors.Grey.Darken1);
            c.Item().Text(valor).FontSize(16).Bold();
        });

    private static void Tabla(ColumnDescriptor col, string titulo, string[] cabecera, IEnumerable<string[]> filas)
    {
        var lista = filas.ToList();
        col.Item().Column(c =>
        {
            c.Item().PaddingBottom(4).Text(titulo).FontSize(13).Bold();
            if (lista.Count == 0) { c.Item().Text("Sin datos en el período.").FontColor(Colors.Grey.Medium); return; }

            c.Item().Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    for (var i = 0; i < cabecera.Length; i++)
                    {
                        if (i == 0 || (cabecera.Length == 4 && i == 2)) cd.RelativeColumn(3); else cd.RelativeColumn(1.4f);
                    }
                });
                t.Header(h =>
                {
                    foreach (var texto in cabecera)
                        h.Cell().Background("#E6F5FD").Padding(5).Text(texto).Bold();
                });
                foreach (var fila in lista)
                    foreach (var celda in fila)
                        t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(celda);
            });
        });
    }
}
