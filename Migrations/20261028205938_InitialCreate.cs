using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Cevicheria.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categorias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categorias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mesas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NumeroMesa = table.Column<int>(type: "integer", nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mesas", x => x.Id);
                    table.CheckConstraint("ck_mesas_estado", "\"Estado\" IN ('Libre','Ocupada','Por pagar')");
                });

            migrationBuilder.CreateTable(
                name: "platos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CategoriaId = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Precio = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Disponible = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platos", x => x.Id);
                    table.CheckConstraint("ck_platos_precio", "\"Precio\" >= 0");
                    table.ForeignKey(
                        name: "FK_platos_categorias_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "categorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pedidos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MesaId = table.Column<int>(type: "integer", nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    MetodoPago = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pedidos", x => x.Id);
                    table.CheckConstraint("ck_pedidos_estado", "\"Estado\" IN ('Pendiente','En preparación','Entregado','Pagado')");
                    table.CheckConstraint("ck_pedidos_metodo_pago", "\"MetodoPago\" IS NULL OR \"MetodoPago\" IN ('Efectivo','Yape','Plin','Tarjeta')");
                    table.ForeignKey(
                        name: "FK_pedidos_mesas_MesaId",
                        column: x => x.MesaId,
                        principalTable: "mesas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "detalles_pedido",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PedidoId = table.Column<int>(type: "integer", nullable: false),
                    PlatoId = table.Column<int>(type: "integer", nullable: false),
                    Cantidad = table.Column<int>(type: "integer", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Observaciones = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_detalles_pedido", x => x.Id);
                    table.CheckConstraint("ck_detalles_cantidad", "\"Cantidad\" > 0");
                    table.ForeignKey(
                        name: "FK_detalles_pedido_pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_detalles_pedido_platos_PlatoId",
                        column: x => x.PlatoId,
                        principalTable: "platos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "categorias",
                columns: new[] { "Id", "Activo", "Nombre" },
                values: new object[,]
                {
                    { 1, true, "Ceviches" },
                    { 2, true, "Entradas" },
                    { 3, true, "Chicharrones" },
                    { 4, true, "Bebidas" }
                });

            migrationBuilder.InsertData(
                table: "mesas",
                columns: new[] { "Id", "Estado", "NumeroMesa" },
                values: new object[,]
                {
                    { 1, "Libre", 1 },
                    { 2, "Libre", 2 },
                    { 3, "Libre", 3 },
                    { 4, "Libre", 4 },
                    { 5, "Libre", 5 },
                    { 6, "Libre", 6 },
                    { 7, "Libre", 7 },
                    { 8, "Libre", 8 },
                    { 9, "Libre", 9 },
                    { 10, "Libre", 10 }
                });

            migrationBuilder.InsertData(
                table: "platos",
                columns: new[] { "Id", "CategoriaId", "Descripcion", "Disponible", "Nombre", "Precio" },
                values: new object[,]
                {
                    { 1, 1, "Pescado fresco, calamar, pulpo y langostinos en leche de tigre", true, "Ceviche Mixto", 42.00m },
                    { 2, 2, "Causa limeña rellena de atún con palta y mayonesa", true, "Causa de Atún", 22.00m },
                    { 3, 3, "Calamar crocante con salsa tártara y yucas fritas", true, "Chicharrón de Calamar", 38.00m },
                    { 4, 4, "Jarra de chicha morada natural (1 litro)", true, "Chicha Morada", 14.00m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_categorias_Nombre",
                table: "categorias",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_detalles_pedido_PedidoId",
                table: "detalles_pedido",
                column: "PedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_detalles_pedido_PlatoId",
                table: "detalles_pedido",
                column: "PlatoId");

            migrationBuilder.CreateIndex(
                name: "IX_mesas_NumeroMesa",
                table: "mesas",
                column: "NumeroMesa",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pedidos_MesaId_Estado",
                table: "pedidos",
                columns: new[] { "MesaId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_platos_CategoriaId",
                table: "platos",
                column: "CategoriaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "detalles_pedido");

            migrationBuilder.DropTable(
                name: "pedidos");

            migrationBuilder.DropTable(
                name: "platos");

            migrationBuilder.DropTable(
                name: "mesas");

            migrationBuilder.DropTable(
                name: "categorias");
        }
    }
}
