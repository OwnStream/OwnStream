using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OwnStream.Migrations
{
    /// <inheritdoc />
    public partial class SeparateExternalIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentExternalIds",
                columns: table => new
                {
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<string>(type: "text", nullable: false),
                    ExternalId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentExternalIds", x => new { x.ContentId, x.ProviderId });
                    table.ForeignKey(
                        name: "FK_ContentExternalIds_Content_ContentId",
                        column: x => x.ContentId,
                        principalTable: "Content",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
            
            migrationBuilder.Sql("""
                                 	INSERT INTO "ContentExternalIds" ("ContentId", "ProviderId", "ExternalId")
                                 	SELECT "Id", 'imdb', "ImdbId"
                                 	FROM "Content"
                                 	WHERE "ImdbId" IS NOT NULL
                                 """);

            migrationBuilder.Sql("""
                                 	INSERT INTO "ContentExternalIds" ("ContentId", "ProviderId", "ExternalId")
                                 	SELECT "Id", 'tmdb', "TmdbId"::text
                                 	FROM "Content"
                                 	WHERE "TmdbId" IS NOT NULL
                                 """);

            migrationBuilder.Sql("""
                                 	INSERT INTO "ContentExternalIds" ("ContentId", "ProviderId", "ExternalId")
                                 	SELECT "Id", 'tvdb', "TvdbId"::text
                                 	FROM "Content"
                                 	WHERE "TvdbId" IS NOT NULL
                                 """);

            migrationBuilder.Sql("""
                                 	INSERT INTO "ContentExternalIds" ("ContentId", "ProviderId", "ExternalId")
                                 	SELECT "Id", 'tvMaze', "TvMazeId"::text
                                 	FROM "Content"
                                 	WHERE "TvMazeId" IS NOT NULL
                                 """);

            migrationBuilder.DropColumn(
                name: "ImdbId",
                table: "Content");

            migrationBuilder.DropColumn(
                name: "TmdbId",
                table: "Content");

            migrationBuilder.DropColumn(
                name: "TvMazeId",
                table: "Content");

            migrationBuilder.DropColumn(
                name: "TvdbId",
                table: "Content");

            migrationBuilder.CreateIndex(
                name: "IX_ContentExternalIds_ProviderId_ExternalId",
                table: "ContentExternalIds",
                columns: new[] { "ProviderId", "ExternalId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentExternalIds");

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

            migrationBuilder.AddColumn<int>(
                name: "TvMazeId",
                table: "Content",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TvdbId",
                table: "Content",
                type: "integer",
                nullable: true);
        }
    }
}
