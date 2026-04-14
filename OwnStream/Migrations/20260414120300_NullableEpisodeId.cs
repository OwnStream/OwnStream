using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OwnStream.Migrations
{
    /// <inheritdoc />
    public partial class NullableEpisodeId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Videos_Episode_EpisodeId",
                table: "Videos");

            migrationBuilder.AlterColumn<Guid>(
                name: "EpisodeId",
                table: "Videos",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_Videos_Episode_EpisodeId",
                table: "Videos",
                column: "EpisodeId",
                principalTable: "Episode",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Videos_Episode_EpisodeId",
                table: "Videos");

            migrationBuilder.AlterColumn<Guid>(
                name: "EpisodeId",
                table: "Videos",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Videos_Episode_EpisodeId",
                table: "Videos",
                column: "EpisodeId",
                principalTable: "Episode",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
