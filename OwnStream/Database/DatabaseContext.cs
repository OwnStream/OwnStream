using System.Text;
using Microsoft.EntityFrameworkCore;
using OwnStream.Database.Models;

namespace OwnStream.Database;

public class DatabaseContext : DbContext
{
	public DbSet<DatabaseUser> Users { get; set; }
	public DbSet<DatabaseVideo> Videos { get; set; }
	public DbSet<DatabaseLibrary> Libraries { get; set; }
	public DbSet<DatabaseWebhook> Webhooks { get; set; }

	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
	{
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
		optionsBuilder.UseNpgsql(sb.ToString());
	}

	public bool IsSetup() => Users.Any();
}