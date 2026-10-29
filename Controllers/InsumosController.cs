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
public class InsumosController(AppDbContext db) : ControllerBase
{
    private static InsumoDto Dto(Insumo i) => new(i.Id, i.Nombre, i.Unidad, i.Stock, i.StockMinimo);

    /// <summary>Lista los insumos, los de menor stock relativo primero.</summary>
    [HttpGet]
    public async Task<ActionResult<List<InsumoDto>>> Get(CancellationToken ct) =>
        await db.Insumos.AsNoTracking()
            .OrderBy(i => i.Stock - i.StockMinimo).ThenBy(i => i.Nombre)
            .Select(i => new InsumoDto(i.Id, i.Nombre, i.Unidad, i.Stock, i.StockMinimo))
            .ToListAsync(ct);

    [HttpPost]
    public async Task<ActionResult<InsumoDto>> Crear(CrearInsumoRequest req, CancellationToken ct)
    {
        var nombre = req.Nombre.Trim();
        if (await db.Insumos.AnyAsync(i => i.Nombre == nombre, ct))
            return BadRequest($"Ya existe el insumo '{nombre}'.");

        var insumo = new Insumo
        {
            Nombre = nombre,
            Unidad = req.Unidad.Trim(),
            Stock = req.Stock,
            StockMinimo = req.StockMinimo
        };
        db.Insumos.Add(insumo);
        await db.SaveChangesAsync(ct);
        return Dto(insumo);
    }

    /// <summary>Suma (cantidad positiva) o descuenta (negativa) stock. No permite quedar bajo cero.</summary>
    [HttpPut("{id:int}/ajustar")]
    public async Task<ActionResult<InsumoDto>> Ajustar(int id, AjustarStockRequest req, CancellationToken ct)
    {
        // Actualización atómica: dos ajustes simultáneos no se pisan.
        var filas = await db.Insumos
            .Where(i => i.Id == id && i.Stock + req.Cantidad >= 0)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Stock, i => i.Stock + req.Cantidad)
                .SetProperty(i => i.UltimaActualizacion, DateTime.UtcNow), ct);

        var insumo = await db.Insumos.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
        if (insumo is null) return NotFound();
        if (filas == 0) return BadRequest($"No hay suficiente stock de '{insumo.Nombre}' (quedan {insumo.Stock:0.##} {insumo.Unidad}).");
        return Dto(insumo);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        var filas = await db.Insumos.Where(i => i.Id == id).ExecuteDeleteAsync(ct);
        return filas == 0 ? NotFound() : NoContent();
    }
}
