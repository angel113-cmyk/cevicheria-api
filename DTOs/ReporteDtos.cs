using System.ComponentModel.DataAnnotations;

namespace Cevicheria.Api.DTOs;

public record AbrirCajaRequest([Range(0, 99999999.99)] decimal MontoInicial);

public record MovimientoCajaRequest(
    [Required] string Tipo,
    [Range(0.01, 99999999.99)] decimal Monto,
    [Required, StringLength(200, MinimumLength = 3)] string Concepto);

public record CerrarCajaRequest(
    [Range(0, 99999999.99)] decimal MontoContado,
    [MaxLength(500)] string? Notas);

public record MovimientoCajaDto(int Id, string Tipo, decimal Monto, string Concepto, DateTime Fecha);

public record ResumenCajaDto(
    int Id,
    string Estado,
    string NombreUsuario,
    DateTime FechaApertura,
    DateTime? FechaCierre,
    decimal MontoInicial,
    decimal TotalEfectivo,
    decimal TotalYape,
    decimal TotalPlin,
    decimal TotalTarjeta,
    decimal TotalCobrado,
    int PedidosAtendidos,
    decimal Ingresos,
    decimal Egresos,
    decimal MontoEsperado,
    decimal? MontoContado,
    decimal? Diferencia,
    string? Notas,
    List<MovimientoCajaDto> Movimientos);

public record VentaPorMetodoDto(string Metodo, decimal Total, int Pedidos);
public record VentaPorDiaDto(DateOnly Fecha, decimal Total, int Pedidos);
public record VentaPorTipoDto(string Tipo, decimal Total, int Pedidos);
public record PlatoVendidoDto(string Plato, int Cantidad, decimal Total);

public record VentasReporteDto(
    DateOnly Desde,
    DateOnly Hasta,
    decimal TotalCobrado,
    int PedidosAtendidos,
    decimal TicketPromedio,
    List<VentaPorMetodoDto> PorMetodo,
    List<VentaPorDiaDto> PorDia,
    List<VentaPorTipoDto> PorTipo,
    List<PlatoVendidoDto> TopPlatos);
