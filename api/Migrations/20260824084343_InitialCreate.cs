using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoAnOlympics.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Drivers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaTaiXe = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    HoTen = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NgayTao = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drivers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContainerRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DriverId = table.Column<int>(type: "int", nullable: false),
                    MaContainer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChecksumHopLe = table.Column<bool>(type: "bit", nullable: false),
                    ThoiGianQuet = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DaGhiSheet = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContainerRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContainerRecords_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContainerRecords_DriverId",
                table: "ContainerRecords",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_MaTaiXe",
                table: "Drivers",
                column: "MaTaiXe",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_Username",
                table: "Drivers",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContainerRecords");

            migrationBuilder.DropTable(
                name: "Drivers");
        }
    }
}
