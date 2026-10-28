using Cevicheria.Api.Data;
using Cevicheria.Api.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cevicheria.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlatosController(AppDbContext db) : ControllerBase
{
    /// <summary>Obtiene el menú. Filtra opcionalmente por categoría y/o solo platos disponibles.</summary>
    [HttpGet]
    public async Task<ActionResult<List<PlatoDto>>> Get(
        [FromQuery] int? categoriaId, [FromQuery] bool soloDisponibles = true, CancellationToken ct = default)
    {
        var query = db.Platos.AsNoTracking().Where(p => p.Categoria!.Activo);
        if (categoriaId is not null) query = query.Where(p => p.CategoriaId == categoriaId);
        if (soloDisponibles) query = query.Where(p => p.Disponible);

        var platos = await query
            .OrderBy(p => p.CategoriaId).ThenBy(p => p.Nombre)
            .Select(p => new PlatoDto(p.Id, p.CategoriaId, p.Categoria!.Nombre, p.Nombre, p.Descripcion, p.Precio, p.Disponible))
            .ToListAsync(ct);
        return platos;
    }
}
