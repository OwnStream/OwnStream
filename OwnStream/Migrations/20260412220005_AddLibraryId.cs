using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OwnStream.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LibraryId",
                table: "Videos",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Videos_LibraryId",
                table: "Videos",
                column: "LibraryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Videos_Libraries_LibraryId",
                table: "Videos",
                column: "LibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Videos_Libraries_LibraryId",
                table: "Videos");

            migrationBuilder.DropIndex(
                name: "IX_Videos_LibraryId",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "LibraryId",
                table: "Videos");
        }
    }
}
