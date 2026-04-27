using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OwnStream.Migrations
{
    /// <inheritdoc />
    public partial class FixMoreForeignKeyBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Videos_Episode_EpisodeId",
                table: "Videos");

            migrationBuilder.DropForeignKey(
                name: "FK_WatchProgress_Content_ContentId",
                table: "WatchProgress");

            migrationBuilder.DropForeignKey(
                name: "FK_WatchProgress_Episode_EpisodeId",
                table: "WatchProgress");

            migrationBuilder.AddForeignKey(
                name: "FK_Videos_Episode_EpisodeId",
                table: "Videos",
                column: "EpisodeId",
                principalTable: "Episode",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WatchProgress_Content_ContentId",
                table: "WatchProgress",
                column: "ContentId",
                principalTable: "Content",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WatchProgress_Episode_EpisodeId",
                table: "WatchProgress",
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

            migrationBuilder.DropForeignKey(
                name: "FK_WatchProgress_Content_ContentId",
                table: "WatchProgress");

            migrationBuilder.DropForeignKey(
                name: "FK_WatchProgress_Episode_EpisodeId",
                table: "WatchProgress");

            migrationBuilder.AddForeignKey(
                name: "FK_Videos_Episode_EpisodeId",
                table: "Videos",
                column: "EpisodeId",
                principalTable: "Episode",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WatchProgress_Content_ContentId",
                table: "WatchProgress",
                column: "ContentId",
                principalTable: "Content",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WatchProgress_Episode_EpisodeId",
                table: "WatchProgress",
                column: "EpisodeId",
                principalTable: "Episode",
                principalColumn: "Id");
        }
    }
}
