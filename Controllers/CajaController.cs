using Cevicheria.Api.Data;
using Cevicheria.Api.DTOs;
using Cevicheria.Api.Models;
using Cevicheria.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Cevicheria.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class CajaController(AppDbContext db, ReporteService reportes) : ControllerBase
{
    /// <summary>Caja abierta con sus totales en vivo; 404 si no hay ninguna abierta.</summary>
    [HttpGet("actual")]
    public async Task<ActionResult<ResumenCajaDto>> Actual(CancellationToken ct)
    {
        var id = await db.Cajas.Where(c => c.Estado == EstadosCaja.Abierta).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);
        if (id is null) return NotFound("No hay una caja abierta.");
        return await reportes.ResumenCajaAsync(id.Value, ct) ?? (ActionResult<ResumenCajaDto>)NotFound();
    }

    /// <summary>Abre la caja con el sencillo inicial. Solo puede haber una caja abierta a la vez.</summary>
    [HttpPost("abrir")]
    public async Task<ActionResult<ResumenCajaDto>> Abrir(AbrirCajaRequest req, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirst("sub")?.Value, out var usuarioId)) return Unauthorized();
        if (await db.Cajas.AnyAsync(c => c.Estado == EstadosCaja.Abierta, ct))
            return Conflict("Ya hay una caja abierta. Ciérrela antes de abrir otra.");

        var caja = new Caja
        {
            UsuarioId = usuarioId,
            NombreUsuario = User.FindFirst("name")?.Value ?? "desconocido",
            MontoInicial = Math.Round(req.MontoInicial, 2),
            FechaApertura = DateTime.UtcNow
        };
        db.Cajas.Add(caja);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Conflict("Ya hay una caja abierta.");
        }
        return await reportes.ResumenCajaAsync(caja.Id, ct) ?? throw new InvalidOperationException();
    }

    /// <summary>Registra un ingreso o egreso de efectivo en la caja abierta (ej. compra de hielo).</summary>
    [HttpPost("movimientos")]
    public async Task<ActionResult<ResumenCajaDto>> Movimiento(MovimientoCajaRequest req, CancellationToken ct)
    {
        if (!TiposMovimiento.Todos.Contains(req.Tipo))
            return BadRequest($"Tipo inválido. Valores permitidos: {string.Join(", ", TiposMovimiento.Todos)}.");
        var caja = await db.Cajas.FirstOrDefaultAsync(c => c.Estado == EstadosCaja.Abierta, ct);
        if (caja is null) return Conflict("No hay una caja abierta.");

        db.MovimientosCaja.Add(new MovimientoCaja
        {
            CajaId = caja.Id,
            Tipo = req.Tipo,
            Monto = Math.Round(req.Monto, 2),
            Concepto = req.Concepto.Trim()
        });
        await db.SaveChangesAsync(ct);
        return await reportes.ResumenCajaAsync(caja.Id, ct) ?? throw new InvalidOperationException();
    }

    /// <summary>
    /// Cierra la caja: compara el efectivo contado con el esperado (inicial + cobros en efectivo + ingresos - egresos)
    /// y guarda la diferencia. Lo puede hacer quien la abrió o un Admin.
    /// </summary>
    [HttpPost("cerrar")]
    public async Task<ActionResult<ResumenCajaDto>> Cerrar(CerrarCajaRequest req, CancellationToken ct)
    {
        var caja = await db.Cajas.FirstOrDefaultAsync(c => c.Estado == EstadosCaja.Abierta, ct);
        if (caja is null) return Conflict("No hay una caja abierta.");

        var esAdmin = User.IsInRole(Roles.Admin);
        var esDueño = User.FindFirst("sub")?.Value == caja.UsuarioId.ToString();
        if (!esAdmin && !esDueño) return Forbid();

        var resumen = await reportes.ResumenCajaAsync(caja.Id, ct) ?? throw new InvalidOperationException();
        var contado = Math.Round(req.MontoContado, 2);

        caja.Estado = EstadosCaja.Cerrada;
        caja.FechaCierre = DateTime.UtcNow;
        caja.MontoEsperado = resumen.MontoEsperado;
        caja.MontoContado = contado;
        caja.Diferencia = contado - resumen.MontoEsperado;
        caja.Notas = string.IsNullOrWhiteSpace(req.Notas) ? null : req.Notas.Trim();
        await db.SaveChangesAsync(ct);

        return await reportes.ResumenCajaAsync(caja.Id, ct) ?? throw new InvalidOperationException();
    }

    /// <summary>Historial de cajas (solo Admin), más recientes primero.</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<List<ResumenCajaDto>>> Historial([FromQuery] int limite = 30, CancellationToken ct = default)
    {
        var ids = await db.Cajas.OrderByDescending(c => c.FechaApertura).Take(Math.Clamp(limite, 1, 200))
            .Select(c => c.Id).ToListAsync(ct);
        var lista = new List<ResumenCajaDto>();
        foreach (var id in ids)
            if (await reportes.ResumenCajaAsync(id, ct) is { } r) lista.Add(r);
        return lista;
    }

    [HttpGet("{id:int}/resumen")]
    public async Task<ActionResult<ResumenCajaDto>> Resumen(int id, CancellationToken ct) =>
        await reportes.ResumenCajaAsync(id, ct) is { } r ? r : NotFound();

    /// <summary>PDF del cierre de caja (arqueo).</summary>
    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> Pdf(int id, CancellationToken ct)
    {
        var r = await reportes.ResumenCajaAsync(id, ct);
        if (r is null) return NotFound();
        return File(ExportService.CajaPdf(r), "application/pdf", $"cierre-caja-{id}.pdf");
    }
}
