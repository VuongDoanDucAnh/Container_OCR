using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoAnOlympics.Api.Migrations
{
    /// <inheritdoc />
    public partial class ThemDongBoOfflineChoContainer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnhPublicId",
                table: "ContainerRecords",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "KinhDo",
                table: "ContainerRecords",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MaYeuCau",
                table: "ContainerRecords",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ThoiGianNhanServer",
                table: "ContainerRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ViDo",
                table: "ContainerRecords",
                type: "float",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContainerRecords_MaYeuCau",
                table: "ContainerRecords",
                column: "MaYeuCau",
                unique: true,
                filter: "[MaYeuCau] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContainerRecords_MaYeuCau",
                table: "ContainerRecords");

            migrationBuilder.DropColumn(
                name: "AnhPublicId",
                table: "ContainerRecords");

            migrationBuilder.DropColumn(
                name: "KinhDo",
                table: "ContainerRecords");

            migrationBuilder.DropColumn(
                name: "MaYeuCau",
                table: "ContainerRecords");

            migrationBuilder.DropColumn(
                name: "ThoiGianNhanServer",
                table: "ContainerRecords");

            migrationBuilder.DropColumn(
                name: "ViDo",
                table: "ContainerRecords");
        }
    }
}
