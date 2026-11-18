using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoAnOlympics.Api.Migrations
{
    /// <inheritdoc />
    public partial class ThemEPodChoChuyen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "AnhChuKy",
                table: "PhanCongChuyens",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "AnhGiaoHang",
                table: "PhanCongChuyens",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnhGiaoHangLoaiNoiDung",
                table: "PhanCongChuyens",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NguoiKyTen",
                table: "PhanCongChuyens",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnhChuKy",
                table: "PhanCongChuyens");

            migrationBuilder.DropColumn(
                name: "AnhGiaoHang",
                table: "PhanCongChuyens");

            migrationBuilder.DropColumn(
                name: "AnhGiaoHangLoaiNoiDung",
                table: "PhanCongChuyens");

            migrationBuilder.DropColumn(
                name: "NguoiKyTen",
                table: "PhanCongChuyens");
        }
    }
}
