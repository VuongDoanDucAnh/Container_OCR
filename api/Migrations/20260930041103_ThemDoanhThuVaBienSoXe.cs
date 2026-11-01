using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoAnOlympics.Api.Migrations
{
    /// <inheritdoc />
    public partial class ThemDoanhThuVaBienSoXe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BienSoXe",
                table: "Drivers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DoanhThu",
                table: "DonHangs",
                type: "decimal(18,0)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BienSoXe",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "DoanhThu",
                table: "DonHangs");
        }
    }
}
