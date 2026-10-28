using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cevicheria.Api.Migrations
{
    /// <inheritdoc />
    public partial class PagosTipoPedido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_pedidos_metodo_pago",
                table: "pedidos");

            migrationBuilder.AlterColumn<int>(
                name: "MesaId",
                table: "pedidos",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaAgendada",
                table: "pedidos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoPedido",
                table: "pedidos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Mesa");

            migrationBuilder.CreateTable(
                name: "pagos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PedidoId = table.Column<int>(type: "integer", nullable: false),
                    MetodoPago = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Monto = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    MontoEntregado = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Vuelto = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    NumeroOperacion = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    FechaPago = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pagos", x => x.Id);
                    table.CheckConstraint("ck_pagos_metodo", "\"MetodoPago\" IN ('Efectivo','Yape','Plin','Tarjeta')");
                    table.CheckConstraint("ck_pagos_montos", "\"MontoEntregado\" >= \"Monto\" AND \"Vuelto\" >= 0");
                    table.ForeignKey(
                        name: "FK_pagos_pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Conserva los cobros ya registrados: cada pedido pagado pasa a la tabla pagos antes de borrar la columna.
            migrationBuilder.Sql("""
                INSERT INTO pagos ("PedidoId", "MetodoPago", "Monto", "MontoEntregado", "Vuelto", "FechaPago")
                SELECT "Id", COALESCE("MetodoPago", 'Efectivo'), "Total", "Total", 0, "FechaCreacion"
                FROM pedidos WHERE "Estado" = 'Pagado';
                """);

            migrationBuilder.DropColumn(
                name: "MetodoPago",
                table: "pedidos");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pedidos_agendado",
                table: "pedidos",
                sql: "\"TipoPedido\" <> 'Agendado' OR \"FechaAgendada\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pedidos_tipo",
                table: "pedidos",
                sql: "\"TipoPedido\" IN ('Mesa','Para Llevar','Agendado')");

            migrationBuilder.CreateIndex(
                name: "IX_pagos_FechaPago",
                table: "pagos",
                column: "FechaPago");

            migrationBuilder.CreateIndex(
                name: "IX_pagos_PedidoId",
                table: "pagos",
                column: "PedidoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pagos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pedidos_agendado",
                table: "pedidos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pedidos_tipo",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "FechaAgendada",
                table: "pedidos");

            migrationBuilder.DropColumn(
                name: "TipoPedido",
                table: "pedidos");

            migrationBuilder.AlterColumn<int>(
                name: "MesaId",
                table: "pedidos",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetodoPago",
                table: "pedidos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_pedidos_metodo_pago",
                table: "pedidos",
                sql: "\"MetodoPago\" IS NULL OR \"MetodoPago\" IN ('Efectivo','Yape','Plin','Tarjeta')");
        }
    }
}
