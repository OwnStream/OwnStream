using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OwnStream.Migrations
{
    /// <inheritdoc />
    public partial class AddTranscodeLibraryIdToInputLibraries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TranscodeLibraryId",
                table: "InputLibraries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_InputLibraries_TranscodeLibraryId",
                table: "InputLibraries",
                column: "TranscodeLibraryId");

            migrationBuilder.AddForeignKey(
                name: "FK_InputLibraries_Libraries_TranscodeLibraryId",
                table: "InputLibraries",
                column: "TranscodeLibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InputLibraries_Libraries_TranscodeLibraryId",
                table: "InputLibraries");

            migrationBuilder.DropIndex(
                name: "IX_InputLibraries_TranscodeLibraryId",
                table: "InputLibraries");

            migrationBuilder.DropColumn(
                name: "TranscodeLibraryId",
                table: "InputLibraries");
        }
    }
}
