using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OwnStream.Migrations
{
    /// <inheritdoc />
    public partial class MakeJobForeignKeysNotBlockDeletes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FfmpegJobs_Content_RelevantContentId",
                table: "FfmpegJobs");

            migrationBuilder.DropForeignKey(
                name: "FK_FfmpegJobs_Episode_RelevantEpisodeId",
                table: "FfmpegJobs");

            migrationBuilder.DropForeignKey(
                name: "FK_FfmpegJobs_Libraries_RelevantLibraryId",
                table: "FfmpegJobs");

            migrationBuilder.DropForeignKey(
                name: "FK_FfmpegJobs_Videos_RelevantVideoId",
                table: "FfmpegJobs");

            migrationBuilder.DropForeignKey(
                name: "FK_FfmpegJobs_Webhooks_RelevantWebhookId",
                table: "FfmpegJobs");

            migrationBuilder.AddForeignKey(
                name: "FK_FfmpegJobs_Content_RelevantContentId",
                table: "FfmpegJobs",
                column: "RelevantContentId",
                principalTable: "Content",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_FfmpegJobs_Episode_RelevantEpisodeId",
                table: "FfmpegJobs",
                column: "RelevantEpisodeId",
                principalTable: "Episode",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_FfmpegJobs_Libraries_RelevantLibraryId",
                table: "FfmpegJobs",
                column: "RelevantLibraryId",
                principalTable: "Libraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_FfmpegJobs_Videos_RelevantVideoId",
                table: "FfmpegJobs",
                column: "RelevantVideoId",
                principalTable: "Videos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_FfmpegJobs_Webhooks_RelevantWebhookId",
                table: "FfmpegJobs",
                column: "RelevantWebhookId",
                principalTable: "Webhooks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FfmpegJobs_Content_RelevantContentId",
                table: "FfmpegJobs");

            migrationBuilder.DropForeignKey(
                name: "FK_FfmpegJobs_Episode_RelevantEpisodeId",
                table: "FfmpegJobs");

            migrationBuilder.DropForeignKey(
                name: "FK_FfmpegJobs_Libraries_RelevantLibraryId",
                table: "FfmpegJobs");

            migrationBuilder.DropForeignKey(
                name: "FK_FfmpegJobs_Videos_RelevantVideoId",
                table: "FfmpegJobs");

            migrationBuilder.DropForeignKey(
                name: "FK_FfmpegJobs_Webhooks_RelevantWebhookId",
                table: "FfmpegJobs");

            migrationBuilder.AddForeignKey(
                name: "FK_FfmpegJobs_Content_RelevantContentId",
                table: "FfmpegJobs",
                column: "RelevantContentId",
                principalTable: "Content",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FfmpegJobs_Episode_RelevantEpisodeId",
                table: "FfmpegJobs",
                column: "RelevantEpisodeId",
                principalTable: "Episode",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FfmpegJobs_Libraries_RelevantLibraryId",
                table: "FfmpegJobs",
                column: "RelevantLibraryId",
                principalTable: "Libraries",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FfmpegJobs_Videos_RelevantVideoId",
                table: "FfmpegJobs",
                column: "RelevantVideoId",
                principalTable: "Videos",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FfmpegJobs_Webhooks_RelevantWebhookId",
                table: "FfmpegJobs",
                column: "RelevantWebhookId",
                principalTable: "Webhooks",
                principalColumn: "Id");
        }
    }
}
