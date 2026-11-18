using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoAnOlympics.Api.Migrations
{
    /// <inheritdoc />
    public partial class ThemCanhBaoChuyenVaNhatKy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ChiPhiPhatSinhs",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CanhBaoChuyens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PhanCongChuyenId = table.Column<int>(type: "int", nullable: false),
                    DriverId = table.Column<int>(type: "int", nullable: false),
                    Loai = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MoTa = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    GiaTriDo = table.Column<double>(type: "float", nullable: false),
                    GiaTriMongDoi = table.Column<double>(type: "float", nullable: false),
                    TrangThai = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GhiChuTaiXe = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    GhiChuQuanLy = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ThoiGianTao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ThoiGianXuLy = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanhBaoChuyens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CanhBaoChuyens_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CanhBaoChuyens_PhanCongChuyens_PhanCongChuyenId",
                        column: x => x.PhanCongChuyenId,
                        principalTable: "PhanCongChuyens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NhatKyQuets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DriverId = table.Column<int>(type: "int", nullable: false),
                    MaYeuCau = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ThoiGian = table.Column<DateTime>(type: "datetime2", nullable: false),
                    KetQua = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MaContainerDoc = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DoTreMs = table.Column<int>(type: "int", nullable: false),
                    KichThuocAnhByte = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NhatKyQuets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CanhBaoChuyens_DriverId_TrangThai",
                table: "CanhBaoChuyens",
                columns: new[] { "DriverId", "TrangThai" });

            migrationBuilder.CreateIndex(
                name: "IX_CanhBaoChuyens_PhanCongChuyenId",
                table: "CanhBaoChuyens",
                column: "PhanCongChuyenId");

            migrationBuilder.CreateIndex(
                name: "IX_NhatKyQuets_ThoiGian",
                table: "NhatKyQuets",
                column: "ThoiGian");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CanhBaoChuyens");

            migrationBuilder.DropTable(
                name: "NhatKyQuets");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ChiPhiPhatSinhs");
        }
    }
}
