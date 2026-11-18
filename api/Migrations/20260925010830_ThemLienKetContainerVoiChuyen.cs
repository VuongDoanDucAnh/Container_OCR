using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoAnOlympics.Api.Migrations
{
    /// <inheritdoc />
    public partial class ThemLienKetContainerVoiChuyen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PhanCongChuyenId",
                table: "ContainerRecords",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContainerRecords_PhanCongChuyenId",
                table: "ContainerRecords",
                column: "PhanCongChuyenId");

            migrationBuilder.AddForeignKey(
                name: "FK_ContainerRecords_PhanCongChuyens_PhanCongChuyenId",
                table: "ContainerRecords",
                column: "PhanCongChuyenId",
                principalTable: "PhanCongChuyens",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContainerRecords_PhanCongChuyens_PhanCongChuyenId",
                table: "ContainerRecords");

            migrationBuilder.DropIndex(
                name: "IX_ContainerRecords_PhanCongChuyenId",
                table: "ContainerRecords");

            migrationBuilder.DropColumn(
                name: "PhanCongChuyenId",
                table: "ContainerRecords");
        }
    }
}
