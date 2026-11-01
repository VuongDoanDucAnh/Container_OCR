using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoAnOlympics.Api.Migrations
{
    /// <inheritdoc />
    public partial class ThemChiPhiPhatSinh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChiPhiPhatSinhs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DriverId = table.Column<int>(type: "int", nullable: false),
                    SoTien = table.Column<decimal>(type: "decimal(18,0)", nullable: false),
                    GiaDau = table.Column<decimal>(type: "decimal(18,0)", nullable: false),
                    SoLit = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    GhiChu = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ThoiGianGui = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TrangThai = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ThoiGianGiaiChi = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChiPhiPhatSinhs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChiPhiPhatSinhs_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChiPhiPhatSinhs_DriverId_ThoiGianGui",
                table: "ChiPhiPhatSinhs",
                columns: new[] { "DriverId", "ThoiGianGui" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChiPhiPhatSinhs");
        }
    }
}
