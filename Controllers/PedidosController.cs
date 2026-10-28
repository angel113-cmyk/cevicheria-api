using System.Text.RegularExpressions;
using Cevicheria.Api.Data;
using Cevicheria.Api.DTOs;
using Cevicheria.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Cevicheria.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public partial class PedidosController(AppDbContext db) : ControllerBase
{
    [GeneratedRegex("^[A-Za-z0-9-]{1,30}$")]
    private static partial Regex OperacionRegex();

    /// <summary>
    /// Crea un pedido. Tipos: 'Mesa' (requiere mesaId), 'Para Llevar' (sin mesa) y 'Agendado' (requiere fechaAgendada futura).
    /// El total se calcula con los precios vigentes en la base de datos.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PedidoDto>> Crear(CrearPedidoRequest req, CancellationToken ct)
    {
        if (!TiposPedido.Todos.Contains(req.TipoPedido))
            return BadRequest($"TipoPedido inválido. Valores permitidos: {string.Join(", ", TiposPedido.Todos)}.");

        Mesa? mesa = null;
        DateTime? fechaAgendada = null;

        if (req.TipoPedido == TiposPedido.Mesa)
        {
            if (req.MesaId is null) return BadRequest("Un pedido de tipo 'Mesa' requiere mesaId.");
            mesa = await db.Mesas.FindAsync([req.MesaId.Value], ct);
            if (mesa is null) return NotFound($"La mesa {req.MesaId} no existe.");
            if (await db.Pedidos.AnyAsync(p => p.MesaId == mesa.Id && p.Estado != EstadosPedido.Pagado, ct))
                return Conflict("La mesa ya tiene un pedido activo.");
        }
        else
        {
            if (req.MesaId is not null) return BadRequest("Solo los pedidos de tipo 'Mesa' llevan mesaId.");
            if (req.TipoPedido == TiposPedido.Agendado)
            {
                if (req.FechaAgendada is null) return BadRequest("Un pedido 'Agendado' requiere fechaAgendada.");
                fechaAgendada = req.FechaAgendada.Value.UtcDateTime;
                if (fechaAgendada <= DateTime.UtcNow) return BadRequest("La fechaAgendada debe ser futura.");
            }
        }

        var platoIds = req.Detalles.Select(d => d.PlatoId).Distinct().ToList();
        var platos = await db.Platos.Where(p => platoIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        var pedido = new Pedido
        {
            MesaId = mesa?.Id,
            TipoPedido = req.TipoPedido,
            FechaAgendada = fechaAgendada,
            Estado = EstadosPedido.Pendiente,
            FechaCreacion = DateTime.UtcNow
        };
        foreach (var d in req.Detalles)
        {
            if (!platos.TryGetValue(d.PlatoId, out var plato))
                return BadRequest($"El plato {d.PlatoId} no existe.");
            if (!plato.Disponible)
                return BadRequest($"El plato '{plato.Nombre}' no está disponible.");

            pedido.Detalles.Add(new DetallePedido
            {
                PlatoId = plato.Id,
                Cantidad = d.Cantidad,
                Subtotal = plato.Precio * d.Cantidad,
                Observaciones = d.Observaciones
            });
        }
        pedido.Total = pedido.Detalles.Sum(d => d.Subtotal);
        if (mesa is not null) mesa.Estado = EstadosMesa.Ocupada;

        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync(ct);

        var dto = await ConsultarPedidos().Where(p => p.Id == pedido.Id).Select(Proyeccion).SingleAsync(ct);
        return CreatedAtAction(nameof(ObtenerPorId), new { id = pedido.Id }, dto);
    }

    /// <summary>Obtiene un pedido por Id.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PedidoDto>> ObtenerPorId(int id, CancellationToken ct)
    {
        var dto = await ConsultarPedidos().Where(p => p.Id == id).Select(Proyeccion).FirstOrDefaultAsync(ct);
        return dto is null ? NotFound() : dto;
    }

    /// <summary>Obtiene el pedido activo (no pagado) de una mesa.</summary>
    [HttpGet("mesa/{mesaId:int}")]
    public async Task<ActionResult<PedidoDto>> ObtenerActivoPorMesa(int mesaId, CancellationToken ct)
    {
        var dto = await ConsultarPedidos()
            .Where(p => p.MesaId == mesaId && p.Estado != EstadosPedido.Pagado)
            .OrderByDescending(p => p.FechaCreacion)
            .Select(Proyeccion)
            .FirstOrDefaultAsync(ct);
        return dto is null ? NotFound() : dto;
    }

    /// <summary>Lista pedidos no pagados, opcionalmente por tipo (ej. ?tipo=Agendado). Los agendados salen por hora.</summary>
    [HttpGet("activos")]
    public async Task<ActionResult<List<PedidoDto>>> Activos([FromQuery] string? tipo, CancellationToken ct)
    {
        var q = ConsultarPedidos().Where(p => p.Estado != EstadosPedido.Pagado);
        if (tipo is not null) q = q.Where(p => p.TipoPedido == tipo);
        return await q.OrderBy(p => p.FechaAgendada ?? p.FechaCreacion).Select(Proyeccion).ToListAsync(ct);
    }

    /// <summary>Actualiza el estado ('En preparación', 'Entregado'). Pagar requiere el endpoint /pagar.</summary>
    [HttpPut("{id:int}/estado")]
    public async Task<ActionResult<PedidoDto>> CambiarEstado(int id, CambiarEstadoPedidoRequest req, CancellationToken ct)
    {
        string[] permitidos = [EstadosPedido.EnPreparacion, EstadosPedido.Entregado];
        if (req.Estado == EstadosPedido.Pagado)
            return BadRequest("Para marcar como 'Pagado' use POST /api/pedidos/{id}/pagar.");
        if (!permitidos.Contains(req.Estado))
            return BadRequest($"Estado inválido. Valores permitidos: {string.Join(", ", permitidos)}.");

        var pedido = await db.Pedidos.Include(p => p.Mesa).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (pedido is null) return NotFound();
        if (pedido.Estado == EstadosPedido.Pagado) return Conflict("El pedido ya fue pagado.");

        pedido.Estado = req.Estado;
        // Al entregar, la mesa queda esperando la cuenta.
        if (req.Estado == EstadosPedido.Entregado && pedido.Mesa is not null)
            pedido.Mesa.Estado = EstadosMesa.PorPagar;

        await db.SaveChangesAsync(ct);
        return await ConsultarPedidos().Where(p => p.Id == id).Select(Proyeccion).SingleAsync(ct);
    }

    /// <summary>
    /// Registra el cobro: marca el pedido 'Pagado', guarda método/monto/vuelto/operación y libera la mesa (si tiene).
    /// Efectivo exige montoEntregado >= total y calcula el vuelto; Yape/Plin/Tarjeta cobran el total exacto.
    /// </summary>
    [HttpPost("{id:int}/pagar")]
    public async Task<ActionResult<PedidoDto>> Pagar(int id, PagarPedidoRequest req, CancellationToken ct)
    {
        if (!MetodosPago.Todos.Contains(req.MetodoPago))
            return BadRequest($"Método de pago inválido. Valores permitidos: {string.Join(", ", MetodosPago.Todos)}.");

        var pedido = await db.Pedidos.Include(p => p.Mesa).Include(p => p.Pago)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (pedido is null) return NotFound();
        if (pedido.Estado == EstadosPedido.Pagado || pedido.Pago is not null)
            return Conflict("El pedido ya fue pagado.");

        decimal entregado = pedido.Total;
        if (req.MetodoPago == MetodosPago.Efectivo)
        {
            if (req.MontoEntregado is null)
                return BadRequest("Para pagos en Efectivo indique montoEntregado.");
            entregado = Math.Round(req.MontoEntregado.Value, 2);
            if (entregado < pedido.Total)
                return BadRequest($"El monto entregado (S/ {entregado:F2}) es menor al total (S/ {pedido.Total:F2}).");
        }

        var operacion = req.NumeroOperacion?.Trim();
        if (string.IsNullOrEmpty(operacion) || req.MetodoPago == MetodosPago.Efectivo) operacion = null;
        else if (!OperacionRegex().IsMatch(operacion))
            return BadRequest("NumeroOperacion solo admite letras, números y guiones (máx. 30).");

        pedido.Pago = new Pago
        {
            MetodoPago = req.MetodoPago,
            Monto = pedido.Total,
            MontoEntregado = entregado,
            Vuelto = entregado - pedido.Total,
            NumeroOperacion = operacion,
            FechaPago = DateTime.UtcNow
        };
        pedido.Estado = EstadosPedido.Pagado;
        if (pedido.Mesa is not null) pedido.Mesa.Estado = EstadosMesa.Libre;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Conflict("El pedido ya fue pagado."); // dos cobros simultáneos
        }
        return await ConsultarPedidos().Where(p => p.Id == id).Select(Proyeccion).SingleAsync(ct);
    }

    private IQueryable<Pedido> ConsultarPedidos() => db.Pedidos.AsNoTracking();

    private static readonly System.Linq.Expressions.Expression<Func<Pedido, PedidoDto>> Proyeccion = p =>
        new PedidoDto(
            p.Id, p.MesaId,
            p.Mesa != null ? p.Mesa.NumeroMesa : (int?)null,
            p.TipoPedido, p.FechaAgendada, p.Estado, p.Total,
            p.Pago != null ? p.Pago.MetodoPago : null,
            p.Pago != null ? p.Pago.MontoEntregado : (decimal?)null,
            p.Pago != null ? p.Pago.Vuelto : (decimal?)null,
            p.Pago != null ? p.Pago.NumeroOperacion : null,
            p.FechaCreacion,
            p.Detalles.OrderBy(d => d.Id)
                .Select(d => new DetallePedidoDto(d.Id, d.PlatoId, d.Plato!.Nombre, d.Plato.Precio, d.Cantidad, d.Subtotal, d.Observaciones))
                .ToList());
}
