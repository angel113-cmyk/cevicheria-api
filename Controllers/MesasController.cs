using Cevicheria.Api.Data;
using Cevicheria.Api.DTOs;
using Cevicheria.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cevicheria.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class MesasController(AppDbContext db) : ControllerBase
{
    /// <summary>Lista todas las mesas con su estado.</summary>
    [HttpGet]
    public async Task<ActionResult<List<MesaDto>>> Get(CancellationToken ct) =>
        await db.Mesas.AsNoTracking().OrderBy(m => m.NumeroMesa)
            .Select(m => new MesaDto(m.Id, m.NumeroMesa, m.Estado))
            .ToListAsync(ct);

    /// <summary>Cambia el estado de una mesa ('Libre', 'Ocupada', 'Por pagar').</summary>
    [HttpPut("{id:int}/estado")]
    public async Task<ActionResult<MesaDto>> CambiarEstado(int id, CambiarEstadoMesaRequest req, CancellationToken ct)
    {
        if (!EstadosMesa.Todos.Contains(req.Estado))
            return BadRequest($"Estado inválido. Valores permitidos: {string.Join(", ", EstadosMesa.Todos)}.");

        var mesa = await db.Mesas.FindAsync([id], ct);
        if (mesa is null) return NotFound();

        mesa.Estado = req.Estado;
        await db.SaveChangesAsync(ct);
        return new MesaDto(mesa.Id, mesa.NumeroMesa, mesa.Estado);
    }
}
