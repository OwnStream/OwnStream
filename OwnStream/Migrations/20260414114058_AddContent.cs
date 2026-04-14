using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OwnStream.Migrations
{
    /// <inheritdoc />
    public partial class AddContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EpisodeId",
                table: "Videos",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Content",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    TranslatedTitle = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    Tagline = table.Column<string>(type: "text", nullable: false),
                    TranslatedTagline = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    TranslatedDescription = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    Poster = table.Column<string>(type: "text", nullable: true),
                    Banner = table.Column<string>(type: "text", nullable: true),
                    Logo = table.Column<string>(type: "text", nullable: true),
                    Backdrop = table.Column<string>(type: "text", nullable: true),
                    Thumbnail = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReleasedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedStreamingAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AgeRatings = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    ExternalIds = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Content", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Content_Libraries_LibraryId",
                        column: x => x.LibraryId,
                        principalTable: "Libraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Episode",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Season = table.Column<int>(type: "integer", nullable: false),
                    Episode = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    TranslatedTitle = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    TranslatedSummary = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    Thumbnail = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReleasedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Episode", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Episode_Content_ParentContentId",
                        column: x => x.ParentContentId,
                        principalTable: "Content",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Videos_EpisodeId",
                table: "Videos",
                column: "EpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Content_ExternalIds",
                table: "Content",
                column: "ExternalIds")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_Content_LibraryId",
                table: "Content",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_Episode_ParentContentId",
                table: "Episode",
                column: "ParentContentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Videos_Episode_EpisodeId",
                table: "Videos",
                column: "EpisodeId",
                principalTable: "Episode",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Videos_Episode_EpisodeId",
                table: "Videos");

            migrationBuilder.DropTable(
                name: "Episode");

            migrationBuilder.DropTable(
                name: "Content");

            migrationBuilder.DropIndex(
                name: "IX_Videos_EpisodeId",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "EpisodeId",
                table: "Videos");
        }
    }
}
