using System.ComponentModel.DataAnnotations;
using Cevicheria.Api.Models;

namespace Cevicheria.Api.DTOs;

public record PlatoDto(int Id, int CategoriaId, string Categoria, string Nombre, string? Descripcion, decimal Precio, bool Disponible);

public record MesaDto(int Id, int NumeroMesa, string Estado);

public class CambiarEstadoMesaRequest
{
    [Required] public string Estado { get; set; } = string.Empty;
}

public class CrearPedidoRequest
{
    /// <summary>'Mesa' (por defecto), 'Para Llevar' o 'Agendado'.</summary>
    public string TipoPedido { get; set; } = TiposPedido.Mesa;

    /// <summary>Obligatorio solo si TipoPedido es 'Mesa'.</summary>
    public int? MesaId { get; set; }

    /// <summary>Obligatorio y futuro si TipoPedido es 'Agendado'. Enviar con zona horaria (ej. 2026-11-02T19:30:00-05:00).</summary>
    public DateTimeOffset? FechaAgendada { get; set; }

    [Required, MinLength(1)]
    public List<CrearDetalleRequest> Detalles { get; set; } = [];
}

public class CrearDetalleRequest
{
    [Range(1, int.MaxValue)] public int PlatoId { get; set; }
    [Range(1, 100)] public int Cantidad { get; set; }
    [MaxLength(300)] public string? Observaciones { get; set; }
}

public class CambiarEstadoPedidoRequest
{
    [Required] public string Estado { get; set; } = string.Empty;
}

public class PagarPedidoRequest
{
    [Required] public string MetodoPago { get; set; } = string.Empty;

    /// <summary>Obligatorio para Efectivo (debe cubrir el total). En otros métodos se asume el total exacto.</summary>
    [Range(0.01, 99999999.99)] public decimal? MontoEntregado { get; set; }

    /// <summary>Dígitos del voucher de Yape/Plin/Tarjeta (opcional).</summary>
    [MaxLength(30)] public string? NumeroOperacion { get; set; }
}

public record ReporteDiarioDto(
    DateOnly Fecha,
    decimal TotalCobrado,
    decimal TotalEfectivo,
    decimal TotalYapePlin,
    decimal TotalYape,
    decimal TotalPlin,
    decimal TotalTarjeta,
    int PedidosAtendidos);

public record DetallePedidoDto(int Id, int PlatoId, string Plato, decimal PrecioUnitario, int Cantidad, decimal Subtotal, string? Observaciones);

public record PedidoDto(
    int Id,
    int? MesaId,
    int? NumeroMesa,
    string TipoPedido,
    DateTime? FechaAgendada,
    string Estado,
    decimal Total,
    string? MetodoPago,
    decimal? MontoEntregado,
    decimal? Vuelto,
    string? NumeroOperacion,
    DateTime FechaCreacion,
    List<DetallePedidoDto> Detalles);

public record CambiarDisponibilidadRequest(bool Disponible);

public record CategoriaDto(int Id, string Nombre);

public class CategoriaRequest
{
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 2)]
    public string Nombre { get; set; } = string.Empty;
}

/// <summary>Datos editables de un plato. El precio es neto (sin IGV).</summary>
public class PlatoRequest
{
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)] public int CategoriaId { get; set; }
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(150, MinimumLength = 2)]
    public string Nombre { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.StringLength(500)] public string? Descripcion { get; set; }
    [System.ComponentModel.DataAnnotations.Range(0, 10000)] public decimal Precio { get; set; }
}

public record InsumoDto(int Id, string Nombre, string Unidad, decimal Stock, decimal StockMinimo);

public class CrearInsumoRequest
{
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 2)]
    public string Nombre { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(20)]
    public string Unidad { get; set; } = "kg";
    [System.ComponentModel.DataAnnotations.Range(0, 100000)] public decimal Stock { get; set; }
    [System.ComponentModel.DataAnnotations.Range(0, 100000)] public decimal StockMinimo { get; set; }
}

/// <summary>Cantidad con signo: positiva suma (entrada), negativa descuenta (consumo).</summary>
public class AjustarStockRequest
{
    [System.ComponentModel.DataAnnotations.Range(-100000, 100000)] public decimal Cantidad { get; set; }
}
