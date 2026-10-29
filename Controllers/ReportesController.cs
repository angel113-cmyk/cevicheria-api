using Cevicheria.Api.Data;
using Cevicheria.Api.DTOs;
using Cevicheria.Api.Models;
using Cevicheria.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cevicheria.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/[controller]")]
public class ReportesController(AppDbContext db, ReporteService reportes) : ControllerBase
{
    /// <summary>Resumen de cobros de un día (por defecto, hoy). Ej: /api/reportes/diario?fecha=2026-11-02</summary>
    [HttpGet("diario")]
    public async Task<ActionResult<ReporteDiarioDto>> Diario([FromQuery] DateOnly? fecha, CancellationToken ct)
    {
        var dia = fecha ?? ReporteService.HoyLima();
        var inicio = new DateTimeOffset(dia.ToDateTime(TimeOnly.MinValue), ReporteService.OffsetLima).UtcDateTime;
        var fin = inicio.AddDays(1);

        var filas = await db.Pagos.AsNoTracking()
            .Where(p => p.FechaPago >= inicio && p.FechaPago < fin)
            .GroupBy(p => p.MetodoPago)
            .Select(g => new { Metodo = g.Key, Total = g.Sum(x => x.Monto), Cantidad = g.Count() })
            .ToListAsync(ct);

        decimal Total(string metodo) => filas.Where(f => f.Metodo == metodo).Sum(f => f.Total);

        return new ReporteDiarioDto(
            dia,
            TotalCobrado: filas.Sum(f => f.Total),
            TotalEfectivo: Total(MetodosPago.Efectivo),
            TotalYapePlin: Total(MetodosPago.Yape) + Total(MetodosPago.Plin),
            TotalYape: Total(MetodosPago.Yape),
            TotalPlin: Total(MetodosPago.Plin),
            TotalTarjeta: Total(MetodosPago.Tarjeta),
            PedidosAtendidos: filas.Sum(f => f.Cantidad));
    }

    /// <summary>Ventas de un rango: total, ticket promedio, por método, por día, por tipo y platos más vendidos.</summary>
    [HttpGet("ventas")]
    public async Task<ActionResult<VentasReporteDto>> Ventas([FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta, CancellationToken ct)
    {
        var (d, h, error) = Rango(desde, hasta);
        return error is not null ? BadRequest(error) : await reportes.VentasAsync(d, h, ct);
    }

    /// <summary>Mismo reporte en Excel (.xlsx), una hoja por sección.</summary>
    [HttpGet("ventas/excel")]
    public async Task<IActionResult> VentasExcel([FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta, CancellationToken ct)
    {
        var (d, h, error) = Rango(desde, hasta);
        if (error is not null) return BadRequest(error);
        var bytes = ExportService.VentasExcel(await reportes.VentasAsync(d, h, ct));
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"ventas-{d:yyyyMMdd}-{h:yyyyMMdd}.xlsx");
    }

    /// <summary>Mismo reporte en PDF.</summary>
    [HttpGet("ventas/pdf")]
    public async Task<IActionResult> VentasPdf([FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta, CancellationToken ct)
    {
        var (d, h, error) = Rango(desde, hasta);
        if (error is not null) return BadRequest(error);
        var bytes = ExportService.VentasPdf(await reportes.VentasAsync(d, h, ct));
        return File(bytes, "application/pdf", $"ventas-{d:yyyyMMdd}-{h:yyyyMMdd}.pdf");
    }

    private static (DateOnly Desde, DateOnly Hasta, string? Error) Rango(DateOnly? desde, DateOnly? hasta)
    {
        var h = hasta ?? ReporteService.HoyLima();
        var d = desde ?? h;
        if (d > h) return (d, h, "'desde' no puede ser posterior a 'hasta'.");
        if (h.DayNumber - d.DayNumber >= ReporteService.MaxDiasReporte)
            return (d, h, $"El rango máximo es de {ReporteService.MaxDiasReporte} días.");
        return (d, h, null);
    }
}
