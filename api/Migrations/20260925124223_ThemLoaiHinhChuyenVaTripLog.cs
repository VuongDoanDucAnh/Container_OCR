using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoAnOlympics.Api.Migrations
{
    /// <inheritdoc />
    public partial class ThemLoaiHinhChuyenVaTripLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiaDiem",
                table: "ContainerRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoaiHinhChuyenId",
                table: "ContainerRecords",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LoaiHinhChuyens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ten = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DonGia = table.Column<decimal>(type: "decimal(18,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoaiHinhChuyens", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContainerRecords_LoaiHinhChuyenId",
                table: "ContainerRecords",
                column: "LoaiHinhChuyenId");

            migrationBuilder.AddForeignKey(
                name: "FK_ContainerRecords_LoaiHinhChuyens_LoaiHinhChuyenId",
                table: "ContainerRecords",
                column: "LoaiHinhChuyenId",
                principalTable: "LoaiHinhChuyens",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContainerRecords_LoaiHinhChuyens_LoaiHinhChuyenId",
                table: "ContainerRecords");

            migrationBuilder.DropTable(
                name: "LoaiHinhChuyens");

            migrationBuilder.DropIndex(
                name: "IX_ContainerRecords_LoaiHinhChuyenId",
                table: "ContainerRecords");

            migrationBuilder.DropColumn(
                name: "DiaDiem",
                table: "ContainerRecords");

            migrationBuilder.DropColumn(
                name: "LoaiHinhChuyenId",
                table: "ContainerRecords");
        }
    }
}
