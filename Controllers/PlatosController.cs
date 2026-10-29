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

    [HttpGet("categorias")]
    public async Task<ActionResult<List<CategoriaDto>>> GetCategorias(CancellationToken ct) =>
        await db.Categorias.AsNoTracking().Where(c => c.Activo).OrderBy(c => c.Id)
            .Select(c => new CategoriaDto(c.Id, c.Nombre)).ToListAsync(ct);

    [HttpPost("categorias")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<CategoriaDto>> CrearCategoria(CategoriaRequest req, CancellationToken ct)
    {
        var nombre = req.Nombre.Trim();
        if (await db.Categorias.AnyAsync(c => c.Nombre == nombre, ct))
            return BadRequest($"Ya existe la categoría '{nombre}'.");
        var cat = new Categoria { Nombre = nombre };
        db.Categorias.Add(cat);
        await db.SaveChangesAsync(ct);
        return new CategoriaDto(cat.Id, cat.Nombre);
    }

    /// <summary>Crea un plato (solo Admin).</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<PlatoDto>> Crear(PlatoRequest req, CancellationToken ct)
    {
        var cat = await db.Categorias.FindAsync([req.CategoriaId], ct);
        if (cat is null) return BadRequest("La categoría no existe.");

        var plato = new Plato
        {
            CategoriaId = cat.Id,
            Nombre = req.Nombre.Trim(),
            Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim(),
            Precio = req.Precio,
            Disponible = true
        };
        db.Platos.Add(plato);
        await db.SaveChangesAsync(ct);
        return new PlatoDto(plato.Id, cat.Id, cat.Nombre, plato.Nombre, plato.Descripcion, plato.Precio, plato.Disponible);
    }

    /// <summary>Edita nombre, descripción, precio y categoría de un plato (solo Admin). Los pedidos ya hechos conservan su subtotal.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<PlatoDto>> Editar(int id, PlatoRequest req, CancellationToken ct)
    {
        var plato = await db.Platos.FindAsync([id], ct);
        if (plato is null) return NotFound();
        var cat = await db.Categorias.FindAsync([req.CategoriaId], ct);
        if (cat is null) return BadRequest("La categoría no existe.");

        plato.CategoriaId = cat.Id;
        plato.Nombre = req.Nombre.Trim();
        plato.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        plato.Precio = req.Precio;
        await db.SaveChangesAsync(ct);
        return new PlatoDto(plato.Id, cat.Id, cat.Nombre, plato.Nombre, plato.Descripcion, plato.Precio, plato.Disponible);
    }

    /// <summary>Elimina un plato sin ventas (solo Admin). Si ya se vendió, hay que marcarlo como agotado.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        if (await db.DetallesPedido.AnyAsync(d => d.PlatoId == id, ct))
            return Conflict("Este plato ya tiene pedidos registrados y no se puede eliminar. Márquelo como agotado.");
        var filas = await db.Platos.Where(p => p.Id == id).ExecuteDeleteAsync(ct);
        return filas == 0 ? NotFound() : NoContent();
    }

    /// <summary>Marca un plato como disponible o agotado (solo Admin). Los pedidos nuevos rechazan los agotados.</summary>
    [HttpPut("{id:int}/disponible")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<PlatoDto>> CambiarDisponibilidad(int id, CambiarDisponibilidadRequest req, CancellationToken ct)
    {
        var plato = await db.Platos.Include(p => p.Categoria).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plato is null) return NotFound();

        plato.Disponible = req.Disponible;
        await db.SaveChangesAsync(ct);
        return new PlatoDto(plato.Id, plato.CategoriaId, plato.Categoria!.Nombre, plato.Nombre, plato.Descripcion, plato.Precio, plato.Disponible);
    }
}
