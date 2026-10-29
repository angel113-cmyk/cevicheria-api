using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Cevicheria.Api.Migrations
{
    /// <inheritdoc />
    public partial class CajaYReportes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CajaId",
                table: "pagos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cajas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    NombreUsuario = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Estado = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FechaApertura = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MontoInicial = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    FechaCierre = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MontoContado = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    MontoEsperado = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    Diferencia = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    Notas = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cajas", x => x.Id);
                    table.CheckConstraint("ck_cajas_estado", "\"Estado\" IN ('Abierta','Cerrada')");
                    table.CheckConstraint("ck_cajas_inicial", "\"MontoInicial\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "movimientos_caja",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CajaId = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Monto = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Concepto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movimientos_caja", x => x.Id);
                    table.CheckConstraint("ck_movimientos_monto", "\"Monto\" > 0");
                    table.CheckConstraint("ck_movimientos_tipo", "\"Tipo\" IN ('Ingreso','Egreso')");
                    table.ForeignKey(
                        name: "FK_movimientos_caja_cajas_CajaId",
                        column: x => x.CajaId,
                        principalTable: "cajas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pagos_CajaId",
                table: "pagos",
                column: "CajaId");

            migrationBuilder.CreateIndex(
                name: "IX_cajas_Estado",
                table: "cajas",
                column: "Estado",
                unique: true,
                filter: "\"Estado\" = 'Abierta'");

            migrationBuilder.CreateIndex(
                name: "IX_cajas_FechaApertura",
                table: "cajas",
                column: "FechaApertura");

            migrationBuilder.CreateIndex(
                name: "IX_movimientos_caja_CajaId",
                table: "movimientos_caja",
                column: "CajaId");

            migrationBuilder.AddForeignKey(
                name: "FK_pagos_cajas_CajaId",
                table: "pagos",
                column: "CajaId",
                principalTable: "cajas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_pagos_cajas_CajaId",
                table: "pagos");

            migrationBuilder.DropTable(
                name: "movimientos_caja");

            migrationBuilder.DropTable(
                name: "cajas");

            migrationBuilder.DropIndex(
                name: "IX_pagos_CajaId",
                table: "pagos");

            migrationBuilder.DropColumn(
                name: "CajaId",
                table: "pagos");
        }
    }
}
