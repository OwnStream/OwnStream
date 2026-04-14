using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OwnStream.Migrations
{
    /// <inheritdoc />
    public partial class SwitchToSeparateProviderIdColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Content_ExternalIds",
                table: "Content");

            migrationBuilder.DropColumn(
                name: "ExternalIds",
                table: "Content");

            migrationBuilder.AddColumn<string>(
                name: "ImdbId",
                table: "Content",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TmdbId",
                table: "Content",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImdbId",
                table: "Content");

            migrationBuilder.DropColumn(
                name: "TmdbId",
                table: "Content");

            migrationBuilder.AddColumn<Dictionary<string, string>>(
                name: "ExternalIds",
                table: "Content",
                type: "jsonb",
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "IX_Content_ExternalIds",
                table: "Content",
                column: "ExternalIds")
                .Annotation("Npgsql:IndexMethod", "gin");
        }
    }
}
