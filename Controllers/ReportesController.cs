using Cevicheria.Api.Data;
using Cevicheria.Api.DTOs;
using Cevicheria.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cevicheria.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/[controller]")]
public class ReportesController(AppDbContext db) : ControllerBase
{
    // Perú no usa horario de verano: UTC-5 fijo. El "día" del reporte va de 00:00 a 24:00 hora de Lima.
    private static readonly TimeSpan OffsetLima = TimeSpan.FromHours(-5);

    /// <summary>Resumen de cobros de un día (por defecto, hoy). Ej: /api/reportes/diario?fecha=2026-11-02</summary>
    [HttpGet("diario")]
    public async Task<ActionResult<ReporteDiarioDto>> Diario([FromQuery] DateOnly? fecha, CancellationToken ct)
    {
        var dia = fecha ?? DateOnly.FromDateTime(DateTime.UtcNow + OffsetLima);
        var inicio = new DateTimeOffset(dia.ToDateTime(TimeOnly.MinValue), OffsetLima).UtcDateTime;
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
}
