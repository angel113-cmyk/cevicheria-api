using Cevicheria.Api.Data;
using Cevicheria.Api.DTOs;
using Cevicheria.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Cevicheria.Api.Services;

/// <summary>Consultas de caja y ventas. Los "días" se cuentan en hora de Lima (UTC-5 fijo, sin horario de verano).</summary>
public class ReporteService(AppDbContext db)
{
    public static readonly TimeSpan OffsetLima = TimeSpan.FromHours(-5);
    public const int MaxDiasReporte = 366;

    public static DateOnly HoyLima() => DateOnly.FromDateTime(DateTime.UtcNow + OffsetLima);

    private static DateTime InicioUtc(DateOnly dia) =>
        new DateTimeOffset(dia.ToDateTime(TimeOnly.MinValue), OffsetLima).UtcDateTime;

    public async Task<ResumenCajaDto?> ResumenCajaAsync(int cajaId, CancellationToken ct)
    {
        var caja = await db.Cajas.AsNoTracking().Include(c => c.Movimientos).FirstOrDefaultAsync(c => c.Id == cajaId, ct);
        if (caja is null) return null;

        var filas = await db.Pagos.AsNoTracking().Where(p => p.CajaId == cajaId)
            .GroupBy(p => p.MetodoPago)
            .Select(g => new { Metodo = g.Key, Total = g.Sum(x => x.Monto), Cantidad = g.Count() })
            .ToListAsync(ct);

        decimal Total(string m) => filas.Where(f => f.Metodo == m).Sum(f => f.Total);
        var ingresos = caja.Movimientos.Where(m => m.Tipo == TiposMovimiento.Ingreso).Sum(m => m.Monto);
        var egresos = caja.Movimientos.Where(m => m.Tipo == TiposMovimiento.Egreso).Sum(m => m.Monto);
        var esperado = caja.MontoEsperado ?? caja.MontoInicial + Total(MetodosPago.Efectivo) + ingresos - egresos;

        return new ResumenCajaDto(
            caja.Id, caja.Estado, caja.NombreUsuario, caja.FechaApertura, caja.FechaCierre, caja.MontoInicial,
            Total(MetodosPago.Efectivo), Total(MetodosPago.Yape), Total(MetodosPago.Plin), Total(MetodosPago.Tarjeta),
            filas.Sum(f => f.Total), filas.Sum(f => f.Cantidad), ingresos, egresos, esperado,
            caja.MontoContado, caja.Diferencia, caja.Notas,
            caja.Movimientos.OrderBy(m => m.Fecha)
                .Select(m => new MovimientoCajaDto(m.Id, m.Tipo, m.Monto, m.Concepto, m.Fecha)).ToList());
    }

    public async Task<VentasReporteDto> VentasAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var inicio = InicioUtc(desde);
        var fin = InicioUtc(hasta).AddDays(1);

        var pagos = await db.Pagos.AsNoTracking()
            .Where(p => p.FechaPago >= inicio && p.FechaPago < fin)
            .Select(p => new { p.FechaPago, p.MetodoPago, p.Monto, Tipo = p.Pedido!.TipoPedido })
            .ToListAsync(ct);

        var top = await db.DetallesPedido.AsNoTracking()
            .Where(d => d.Pedido!.Pago != null && d.Pedido.Pago.FechaPago >= inicio && d.Pedido.Pago.FechaPago < fin)
            .GroupBy(d => d.Plato!.Nombre)
            .Select(g => new PlatoVendidoDto(g.Key, g.Sum(x => x.Cantidad), g.Sum(x => x.Subtotal)))
            .OrderByDescending(x => x.Cantidad).ThenBy(x => x.Plato)
            .Take(10)
            .ToListAsync(ct);

        var total = pagos.Sum(p => p.Monto);
        return new VentasReporteDto(
            desde, hasta, total, pagos.Count,
            pagos.Count == 0 ? 0 : Math.Round(total / pagos.Count, 2),
            pagos.GroupBy(p => p.MetodoPago).Select(g => new VentaPorMetodoDto(g.Key, g.Sum(x => x.Monto), g.Count()))
                .OrderByDescending(x => x.Total).ToList(),
            pagos.GroupBy(p => DateOnly.FromDateTime(p.FechaPago + OffsetLima))
                .Select(g => new VentaPorDiaDto(g.Key, g.Sum(x => x.Monto), g.Count())).OrderBy(x => x.Fecha).ToList(),
            pagos.GroupBy(p => p.Tipo).Select(g => new VentaPorTipoDto(g.Key, g.Sum(x => x.Monto), g.Count()))
                .OrderByDescending(x => x.Total).ToList(),
            top);
    }
}
