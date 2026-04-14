using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OwnStream.Migrations
{
    /// <inheritdoc />
    public partial class AddTvdbAndTvMaze : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TvMazeId",
                table: "Content");

            migrationBuilder.DropColumn(
                name: "TvdbId",
                table: "Content");
        }
    }
}
