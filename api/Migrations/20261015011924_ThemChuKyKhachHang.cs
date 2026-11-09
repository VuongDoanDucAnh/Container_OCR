using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoAnOlympics.Api.Migrations
{
    /// <inheritdoc />
    public partial class ThemChuKyKhachHang : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "AnhChuKyKhachHang",
                table: "DonHangs",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnhChuKyLoaiNoiDung",
                table: "DonHangs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ThoiGianKyNhan",
                table: "DonHangs",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnhChuKyKhachHang",
                table: "DonHangs");

            migrationBuilder.DropColumn(
                name: "AnhChuKyLoaiNoiDung",
                table: "DonHangs");

            migrationBuilder.DropColumn(
                name: "ThoiGianKyNhan",
                table: "DonHangs");
        }
    }
}
