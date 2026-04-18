using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OwnStream.Database.Models;

namespace OwnStream.Database;

public class DatabaseContext : DbContext
{
	private static NpgsqlDataSource? dataSource = null;
	public DbSet<DatabaseUser> Users { get; set; }
	public DbSet<DatabaseVideo> Videos { get; set; }
	public DbSet<DatabaseLibrary> Libraries { get; set; }
	public DbSet<DatabaseWebhook> Webhooks { get; set; }
	public DbSet<DatabaseFfmpegJob> FfmpegJobs { get; set; }
	public DbSet<DatabaseContent> Content { get; set; }
	public DbSet<DatabaseEpisode> Episode { get; set; }
	public DbSet<DatabaseWatchProgress> WatchProgress { get; set; }
	public DbSet<DatabaseVideoSegment> VideoSegments { get; set; }

	private static void PrepareDataSource()
	{
		if (dataSource != null) return;
		StringBuilder sb = new();
		// ReSharper disable PassStringInterpolation
		sb.AppendFormat("Host={0}; ", Utils.GetEnvironmentVariable("POSTGRES_HOST"));
		sb.AppendFormat("Database={0}; ", Utils.GetEnvironmentVariable("POSTGRES_DATABASE", "ownstream"));
		sb.AppendFormat("Username={0}; ", Utils.GetEnvironmentVariable("POSTGRES_USERNAME"));
		sb.AppendFormat("Password={0}; ", Utils.GetEnvironmentVariable("POSTGRES_PASSWORD"));
		// ReSharper restore PassStringInterpolation
		if (string.Equals(Utils.GetEnvironmentVariable("POSTGRES_VERBOSE_ERRORS", "false"), "true",
			    StringComparison.InvariantCultureIgnoreCase))
			sb.Append("Include Error Detail=true;");
		sb.Append("Maximum Pool Size=20;");
		sb.Append("Application Name=OwnStream;");

		NpgsqlDataSourceBuilder dataSourceBuilder = new(sb.ToString());
		dataSourceBuilder.EnableDynamicJson();
		dataSource = dataSourceBuilder.Build();
	}

	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
	{
		PrepareDataSource();
		optionsBuilder.UseNpgsql(dataSource!);
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<DatabaseContent>().Property(x => x.TranslatedTitle).HasColumnType("jsonb");
		modelBuilder.Entity<DatabaseContent>().Property(x => x.TranslatedTagline).HasColumnType("jsonb");
		modelBuilder.Entity<DatabaseContent>().Property(x => x.TranslatedDescription).HasColumnType("jsonb");
		modelBuilder.Entity<DatabaseContent>().Property(x => x.AgeRatings).HasColumnType("jsonb");
		modelBuilder.Entity<DatabaseEpisode>().Property(x => x.TranslatedTitle).HasColumnType("jsonb");
		modelBuilder.Entity<DatabaseEpisode>().Property(x => x.TranslatedSummary).HasColumnType("jsonb");

		modelBuilder.Entity<DatabaseFfmpegJob>().HasOne(x => x.RelevantVideo).WithMany().HasForeignKey(x => x.RelevantVideoId).OnDelete(DeleteBehavior.SetNull);
		modelBuilder.Entity<DatabaseFfmpegJob>().HasOne(x => x.RelevantEpisode).WithMany().HasForeignKey(x => x.RelevantEpisodeId).OnDelete(DeleteBehavior.SetNull);
		modelBuilder.Entity<DatabaseFfmpegJob>().HasOne(x => x.RelevantContent).WithMany().HasForeignKey(x => x.RelevantContentId).OnDelete(DeleteBehavior.SetNull);
		modelBuilder.Entity<DatabaseFfmpegJob>().HasOne(x => x.RelevantLibrary).WithMany().HasForeignKey(x => x.RelevantLibraryId).OnDelete(DeleteBehavior.SetNull);
		modelBuilder.Entity<DatabaseFfmpegJob>().HasOne(x => x.RelevantWebhook).WithMany().HasForeignKey(x => x.RelevantWebhookId).OnDelete(DeleteBehavior.SetNull);
	}

	public bool IsSetup() => Users.Any();
}