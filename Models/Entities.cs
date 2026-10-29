namespace Cevicheria.Api.Models;

/// <summary>Valores permitidos para los campos de tipo string con dominio cerrado.</summary>
public static class EstadosMesa
{
    public const string Libre = "Libre";
    public const string Ocupada = "Ocupada";
    public const string PorPagar = "Por pagar";

    public static readonly string[] Todos = [Libre, Ocupada, PorPagar];
}

public static class EstadosPedido
{
    public const string Pendiente = "Pendiente";
    public const string EnPreparacion = "En preparación";
    public const string Entregado = "Entregado";
    public const string Pagado = "Pagado";

    public static readonly string[] Todos = [Pendiente, EnPreparacion, Entregado, Pagado];
}

public static class TiposPedido
{
    public const string Mesa = "Mesa";
    public const string ParaLlevar = "Para Llevar";
    public const string Agendado = "Agendado";

    public static readonly string[] Todos = [Mesa, ParaLlevar, Agendado];
}

public static class MetodosPago
{
    public const string Efectivo = "Efectivo";
    public const string Yape = "Yape";
    public const string Plin = "Plin";
    public const string Tarjeta = "Tarjeta";

    public static readonly string[] Todos = [Efectivo, Yape, Plin, Tarjeta];
}

public class Categoria
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    public ICollection<Plato> Platos { get; set; } = new List<Plato>();
}

public class Plato
{
    public int Id { get; set; }
    public int CategoriaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public bool Disponible { get; set; } = true;

    public Categoria? Categoria { get; set; }
}

public class Mesa
{
    public int Id { get; set; }
    public int NumeroMesa { get; set; }
    public string Estado { get; set; } = EstadosMesa.Libre;
}

public class Pedido
{
    public int Id { get; set; }
    /// <summary>Solo los pedidos de tipo 'Mesa' tienen mesa.</summary>
    public int? MesaId { get; set; }
    public string TipoPedido { get; set; } = TiposPedido.Mesa;
    /// <summary>Fecha y hora fijada (UTC) para pedidos 'Agendado'.</summary>
    public DateTime? FechaAgendada { get; set; }
    public string Estado { get; set; } = EstadosPedido.Pendiente;
    /// <summary>Suma de los subtotales (Precio * Cantidad). Sin IGV ni impuestos.</summary>
    public decimal Total { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public Mesa? Mesa { get; set; }
    public Pago? Pago { get; set; }
    public ICollection<DetallePedido> Detalles { get; set; } = new List<DetallePedido>();
}

/// <summary>Cobro de un pedido (uno por pedido).</summary>
public class Pago
{
    public int Id { get; set; }
    public int PedidoId { get; set; }
    public string MetodoPago { get; set; } = MetodosPago.Efectivo;
    /// <summary>Total del pedido al momento del cobro.</summary>
    public decimal Monto { get; set; }
    public decimal MontoEntregado { get; set; }
    /// <summary>MontoEntregado - Monto.</summary>
    public decimal Vuelto { get; set; }
    /// <summary>Dígitos del voucher de Yape/Plin/Tarjeta.</summary>
    public string? NumeroOperacion { get; set; }
    public DateTime FechaPago { get; set; } = DateTime.UtcNow;
    /// <summary>Caja (turno) abierta en la que se cobró. Nulo en cobros anteriores al módulo de caja.</summary>
    public int? CajaId { get; set; }

    public Pedido? Pedido { get; set; }
    public Caja? Caja { get; set; }
}

public static class EstadosCaja
{
    public const string Abierta = "Abierta";
    public const string Cerrada = "Cerrada";
}

public static class TiposMovimiento
{
    public const string Ingreso = "Ingreso";
    public const string Egreso = "Egreso";

    public static readonly string[] Todos = [Ingreso, Egreso];
}

/// <summary>Turno de caja: apertura con sencillo inicial y cierre con conteo de efectivo.</summary>
public class Caja
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public string NombreUsuario { get; set; } = string.Empty;
    public string Estado { get; set; } = EstadosCaja.Abierta;
    public DateTime FechaApertura { get; set; } = DateTime.UtcNow;
    public decimal MontoInicial { get; set; }
    public DateTime? FechaCierre { get; set; }
    /// <summary>Efectivo contado físicamente al cerrar.</summary>
    public decimal? MontoContado { get; set; }
    /// <summary>Inicial + cobros en efectivo + ingresos - egresos, calculado al cerrar.</summary>
    public decimal? MontoEsperado { get; set; }
    /// <summary>Contado - Esperado (negativo = faltante).</summary>
    public decimal? Diferencia { get; set; }
    public string? Notas { get; set; }

    public ICollection<MovimientoCaja> Movimientos { get; set; } = new List<MovimientoCaja>();
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
}

/// <summary>Ingreso o egreso de efectivo ajeno a las ventas (ej. compra de hielo, retiro).</summary>
public class MovimientoCaja
{
    public int Id { get; set; }
    public int CajaId { get; set; }
    public string Tipo { get; set; } = TiposMovimiento.Egreso;
    public decimal Monto { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    public Caja? Caja { get; set; }
}

public class DetallePedido
{
    public int Id { get; set; }
    public int PedidoId { get; set; }
    public int PlatoId { get; set; }
    public int Cantidad { get; set; }
    public decimal Subtotal { get; set; }
    public string? Observaciones { get; set; }

    public Pedido? Pedido { get; set; }
    public Plato? Plato { get; set; }
}
