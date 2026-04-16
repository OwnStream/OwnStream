using System.ComponentModel.DataAnnotations.Schema;

namespace OwnStream.Database.Models;

public class DatabaseWatchProgress
{
	public Guid Id { get; set; }

	public Guid UserId { get; set; }
	public DatabaseUser User { get; set; } = null!;
	public Guid? ContentId { get; set; }
	public DatabaseContent? Content { get; set; } = null!;
	public Guid? EpisodeId { get; set; }
	public DatabaseEpisode? Episode { get; set; } = null!;
	public Guid VideoId { get; set; }
	public DatabaseVideo Video { get; set; } = null!;

	public int MillisecondsWatched { get; set; }
	public int VideoLength { get; set; }
	public bool FullyWatched { get; set; }
	// ReSharper disable once PossibleLossOfFraction
	[NotMapped] public float WatchPercentage => (MillisecondsWatched / MathF.Max(VideoLength, 1)) * 100f;
	
	public DateTimeOffset UpdatedAt { get; set; }
}