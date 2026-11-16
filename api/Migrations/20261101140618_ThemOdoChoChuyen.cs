using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoAnOlympics.Api.Migrations
{
    /// <inheritdoc />
    public partial class ThemOdoChoChuyen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnhOdoBatDauPublicId",
                table: "PhanCongChuyens",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnhOdoKetThucPublicId",
                table: "PhanCongChuyens",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OdoBatDau",
                table: "PhanCongChuyens",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OdoKetThuc",
                table: "PhanCongChuyens",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnhOdoBatDauPublicId",
                table: "PhanCongChuyens");

            migrationBuilder.DropColumn(
                name: "AnhOdoKetThucPublicId",
                table: "PhanCongChuyens");

            migrationBuilder.DropColumn(
                name: "OdoBatDau",
                table: "PhanCongChuyens");

            migrationBuilder.DropColumn(
                name: "OdoKetThuc",
                table: "PhanCongChuyens");
        }
    }
}
