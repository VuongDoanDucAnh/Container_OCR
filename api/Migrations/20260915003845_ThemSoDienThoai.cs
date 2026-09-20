using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoAnOlympics.Api.Migrations
{
    /// <inheritdoc />
    public partial class ThemSoDienThoai : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SoDienThoai",
                table: "Drivers",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SoDienThoai",
                table: "Drivers");
        }
    }
}
