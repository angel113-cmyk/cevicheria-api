using Cevicheria.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Cevicheria.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Plato> Platos => Set<Plato>();
    public DbSet<Mesa> Mesas => Set<Mesa>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<DetallePedido> DetallesPedido => Set<DetallePedido>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<Caja> Cajas => Set<Caja>();
    public DbSet<MovimientoCaja> MovimientosCaja => Set<MovimientoCaja>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Usuario>(e =>
        {
            e.ToTable("usuarios", t => t.HasCheckConstraint("ck_usuarios_rol", "\"Rol\" IN ('Admin','Mozo')"));
            e.Property(x => x.NombreUsuario).HasMaxLength(50).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(100).IsRequired();
            e.Property(x => x.Rol).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.NombreUsuario).IsUnique();
        });

        b.Entity<Categoria>(e =>
        {
            e.ToTable("categorias");
            e.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Nombre).IsUnique();
        });

        b.Entity<Plato>(e =>
        {
            e.ToTable("platos", t => t.HasCheckConstraint("ck_platos_precio", "\"Precio\" >= 0"));
            e.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(500);
            e.Property(x => x.Precio).HasColumnType("numeric(10,2)");
            e.HasOne(x => x.Categoria).WithMany(c => c.Platos)
                .HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Mesa>(e =>
        {
            e.ToTable("mesas", t => t.HasCheckConstraint("ck_mesas_estado",
                "\"Estado\" IN ('Libre','Ocupada','Por pagar')"));
            e.Property(x => x.Estado).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.NumeroMesa).IsUnique();
        });

        b.Entity<Pedido>(e =>
        {
            e.ToTable("pedidos", t =>
            {
                t.HasCheckConstraint("ck_pedidos_estado",
                    "\"Estado\" IN ('Pendiente','En preparación','Entregado','Pagado')");
                t.HasCheckConstraint("ck_pedidos_tipo", "\"TipoPedido\" IN ('Mesa','Para Llevar','Agendado')");
                t.HasCheckConstraint("ck_pedidos_agendado",
                    "\"TipoPedido\" <> 'Agendado' OR \"FechaAgendada\" IS NOT NULL");
            });
            e.Property(x => x.Estado).HasMaxLength(20).IsRequired();
            e.Property(x => x.TipoPedido).HasMaxLength(20).IsRequired();
            e.Property(x => x.FechaAgendada).HasColumnType("timestamp with time zone");
            e.Property(x => x.Total).HasColumnType("numeric(10,2)");
            e.Property(x => x.FechaCreacion).HasColumnType("timestamp with time zone");
            e.HasOne(x => x.Mesa).WithMany().HasForeignKey(x => x.MesaId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.MesaId, x.Estado });
        });

        b.Entity<Pago>(e =>
        {
            e.ToTable("pagos", t =>
            {
                t.HasCheckConstraint("ck_pagos_metodo", "\"MetodoPago\" IN ('Efectivo','Yape','Plin','Tarjeta')");
                t.HasCheckConstraint("ck_pagos_montos", "\"MontoEntregado\" >= \"Monto\" AND \"Vuelto\" >= 0");
            });
            e.Property(x => x.MetodoPago).HasMaxLength(20).IsRequired();
            e.Property(x => x.Monto).HasColumnType("numeric(10,2)");
            e.Property(x => x.MontoEntregado).HasColumnType("numeric(10,2)");
            e.Property(x => x.Vuelto).HasColumnType("numeric(10,2)");
            e.Property(x => x.NumeroOperacion).HasMaxLength(30);
            e.Property(x => x.FechaPago).HasColumnType("timestamp with time zone");
            e.HasOne(x => x.Pedido).WithOne(p => p.Pago).HasForeignKey<Pago>(x => x.PedidoId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.PedidoId).IsUnique(); // un solo cobro por pedido
            e.HasIndex(x => x.FechaPago);
            e.HasOne(x => x.Caja).WithMany(c => c.Pagos).HasForeignKey(x => x.CajaId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Caja>(e =>
        {
            e.ToTable("cajas", t =>
            {
                t.HasCheckConstraint("ck_cajas_estado", "\"Estado\" IN ('Abierta','Cerrada')");
                t.HasCheckConstraint("ck_cajas_inicial", "\"MontoInicial\" >= 0");
            });
            e.Property(x => x.NombreUsuario).HasMaxLength(50).IsRequired();
            e.Property(x => x.Estado).HasMaxLength(10).IsRequired();
            e.Property(x => x.Notas).HasMaxLength(500);
            e.Property(x => x.MontoInicial).HasColumnType("numeric(10,2)");
            e.Property(x => x.MontoContado).HasColumnType("numeric(10,2)");
            e.Property(x => x.MontoEsperado).HasColumnType("numeric(10,2)");
            e.Property(x => x.Diferencia).HasColumnType("numeric(10,2)");
            e.Property(x => x.FechaApertura).HasColumnType("timestamp with time zone");
            e.Property(x => x.FechaCierre).HasColumnType("timestamp with time zone");
            // Garantiza a nivel de BD que solo exista una caja abierta a la vez.
            e.HasIndex(x => x.Estado).IsUnique().HasFilter("\"Estado\" = 'Abierta'");
            e.HasIndex(x => x.FechaApertura);
        });

        b.Entity<MovimientoCaja>(e =>
        {
            e.ToTable("movimientos_caja", t =>
            {
                t.HasCheckConstraint("ck_movimientos_tipo", "\"Tipo\" IN ('Ingreso','Egreso')");
                t.HasCheckConstraint("ck_movimientos_monto", "\"Monto\" > 0");
            });
            e.Property(x => x.Tipo).HasMaxLength(10).IsRequired();
            e.Property(x => x.Concepto).HasMaxLength(200).IsRequired();
            e.Property(x => x.Monto).HasColumnType("numeric(10,2)");
            e.Property(x => x.Fecha).HasColumnType("timestamp with time zone");
            e.HasOne(x => x.Caja).WithMany(c => c.Movimientos).HasForeignKey(x => x.CajaId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<DetallePedido>(e =>
        {
            e.ToTable("detalles_pedido", t => t.HasCheckConstraint("ck_detalles_cantidad", "\"Cantidad\" > 0"));
            e.Property(x => x.Subtotal).HasColumnType("numeric(10,2)");
            e.Property(x => x.Observaciones).HasMaxLength(300);
            e.HasOne(x => x.Pedido).WithMany(p => p.Detalles)
                .HasForeignKey(x => x.PedidoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Plato).WithMany().HasForeignKey(x => x.PlatoId).OnDelete(DeleteBehavior.Restrict);
        });

        Seed(b);
    }

    private static void Seed(ModelBuilder b)
    {
        b.Entity<Categoria>().HasData(
            new Categoria { Id = 1, Nombre = "Ceviches", Activo = true },
            new Categoria { Id = 2, Nombre = "Entradas", Activo = true },
            new Categoria { Id = 3, Nombre = "Chicharrones", Activo = true },
            new Categoria { Id = 4, Nombre = "Bebidas", Activo = true });

        b.Entity<Plato>().HasData(
            new Plato { Id = 1, CategoriaId = 1, Nombre = "Ceviche Mixto", Descripcion = "Pescado fresco, calamar, pulpo y langostinos en leche de tigre", Precio = 42.00m, Disponible = true },
            new Plato { Id = 2, CategoriaId = 2, Nombre = "Causa de Atún", Descripcion = "Causa limeña rellena de atún con palta y mayonesa", Precio = 22.00m, Disponible = true },
            new Plato { Id = 3, CategoriaId = 3, Nombre = "Chicharrón de Calamar", Descripcion = "Calamar crocante con salsa tártara y yucas fritas", Precio = 38.00m, Disponible = true },
            new Plato { Id = 4, CategoriaId = 4, Nombre = "Chicha Morada", Descripcion = "Jarra de chicha morada natural (1 litro)", Precio = 14.00m, Disponible = true });

        b.Entity<Mesa>().HasData(
            Enumerable.Range(1, 10).Select(n => new Mesa { Id = n, NumeroMesa = n, Estado = EstadosMesa.Libre }));
    }
}
