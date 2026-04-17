using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OwnStream.Migrations
{
    /// <inheritdoc />
    public partial class AddJobProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Progress",
                table: "FfmpegJobs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProgressMax",
                table: "FfmpegJobs",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Progress",
                table: "FfmpegJobs");

            migrationBuilder.DropColumn(
                name: "ProgressMax",
                table: "FfmpegJobs");
        }
    }
}
