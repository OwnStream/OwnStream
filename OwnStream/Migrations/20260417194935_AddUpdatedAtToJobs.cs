using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OwnStream.Migrations
{
    /// <inheritdoc />
    public partial class AddUpdatedAtToJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "FfmpegJobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(@"
                CREATE FUNCTION ""FfmpegJobs_Update_Timestamp_Function""() RETURNS TRIGGER LANGUAGE PLPGSQL AS $$
                BEGIN
                    NEW.""UpdatedAt"" := now();
                    RETURN NEW;
                END;
                $$;
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER ""UpdateTimestamp""
                    BEFORE INSERT OR UPDATE
                    ON ""FfmpegJobs""
                    FOR EACH ROW
                    EXECUTE FUNCTION ""FfmpegJobs_Update_Timestamp_Function""();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS ""UpdateTimestamp"" ON ""FfmpegJobs"";");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS ""FfmpegJobs_Update_Timestamp_Function""();");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "FfmpegJobs");
        }
    }
}
