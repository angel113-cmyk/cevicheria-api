using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Cevicheria.Api.Migrations
{
    /// <inheritdoc />
    public partial class Insumos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "insumos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Unidad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Stock = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    StockMinimo = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    UltimaActualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_insumos", x => x.Id);
                    table.CheckConstraint("ck_insumos_minimo", "\"StockMinimo\" >= 0");
                    table.CheckConstraint("ck_insumos_stock", "\"Stock\" >= 0");
                });

            migrationBuilder.InsertData(
                table: "insumos",
                columns: new[] { "Id", "Nombre", "Stock", "StockMinimo", "UltimaActualizacion", "Unidad" },
                values: new object[,]
                {
                    { 1, "Pescado fresco", 20m, 5m, new DateTime(2026, 10, 29, 0, 0, 0, 0, DateTimeKind.Utc), "kg" },
                    { 2, "Limones", 15m, 5m, new DateTime(2026, 10, 29, 0, 0, 0, 0, DateTimeKind.Utc), "kg" },
                    { 3, "Mariscos", 10m, 3m, new DateTime(2026, 10, 29, 0, 0, 0, 0, DateTimeKind.Utc), "kg" },
                    { 4, "Cebolla", 12m, 4m, new DateTime(2026, 10, 29, 0, 0, 0, 0, DateTimeKind.Utc), "kg" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_insumos_Nombre",
                table: "insumos",
                column: "Nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "insumos");
        }
    }
}
