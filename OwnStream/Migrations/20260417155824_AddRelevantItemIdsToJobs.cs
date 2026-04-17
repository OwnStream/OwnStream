using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OwnStream.Migrations
{
    /// <inheritdoc />
    public partial class AddRelevantItemIdsToJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RelevantContentId",
                table: "FfmpegJobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelevantEpisodeId",
                table: "FfmpegJobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelevantLibraryId",
                table: "FfmpegJobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelevantVideoId",
                table: "FfmpegJobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelevantWebhookId",
                table: "FfmpegJobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FfmpegJobs_RelevantContentId",
                table: "FfmpegJobs",
                column: "RelevantContentId");

            migrationBuilder.CreateIndex(
                name: "IX_FfmpegJobs_RelevantEpisodeId",
                table: "FfmpegJobs",
                column: "RelevantEpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_FfmpegJobs_RelevantLibraryId",
                table: "FfmpegJobs",
                column: "RelevantLibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_FfmpegJobs_RelevantVideoId",
                table: "FfmpegJobs",
                column: "RelevantVideoId");

            migrationBuilder.CreateIndex(
                name: "IX_FfmpegJobs_RelevantWebhookId",
                table: "FfmpegJobs",
                column: "RelevantWebhookId");

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

            migrationBuilder.DropIndex(
                name: "IX_FfmpegJobs_RelevantContentId",
                table: "FfmpegJobs");

            migrationBuilder.DropIndex(
                name: "IX_FfmpegJobs_RelevantEpisodeId",
                table: "FfmpegJobs");

            migrationBuilder.DropIndex(
                name: "IX_FfmpegJobs_RelevantLibraryId",
                table: "FfmpegJobs");

            migrationBuilder.DropIndex(
                name: "IX_FfmpegJobs_RelevantVideoId",
                table: "FfmpegJobs");

            migrationBuilder.DropIndex(
                name: "IX_FfmpegJobs_RelevantWebhookId",
                table: "FfmpegJobs");

            migrationBuilder.DropColumn(
                name: "RelevantContentId",
                table: "FfmpegJobs");

            migrationBuilder.DropColumn(
                name: "RelevantEpisodeId",
                table: "FfmpegJobs");

            migrationBuilder.DropColumn(
                name: "RelevantLibraryId",
                table: "FfmpegJobs");

            migrationBuilder.DropColumn(
                name: "RelevantVideoId",
                table: "FfmpegJobs");

            migrationBuilder.DropColumn(
                name: "RelevantWebhookId",
                table: "FfmpegJobs");
        }
    }
}
