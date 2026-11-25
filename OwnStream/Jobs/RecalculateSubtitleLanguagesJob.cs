using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using OwnStream.Database;
using OwnStream.Database.Models;
using OwnStream.Managers;

namespace OwnStream.Jobs;

[Job("RecalculateSubtitleLanguages")]
public class RecalculateSubtitleLanguagesJob : IJob
{
	private DatabaseContext db = null!;
	private TagManager tm = null!;
	private ILogger<RecalculateSubtitleLanguagesJob> logger = null!;

	public void Initialize(IServiceProvider serviceProvider)
	{
		db = serviceProvider.GetRequiredService<DatabaseContext>();
		tm = serviceProvider.GetRequiredService<TagManager>();
		logger = serviceProvider.GetRequiredService<ILogger<RecalculateSubtitleLanguagesJob>>();
	}

	public async Task ExecuteJob(Guid jobId, CancellationToken cancellationToken)
	{
		DatabaseFfmpegJob? job = await db.FfmpegJobs.FindAsync([jobId], cancellationToken: cancellationToken);
		if (job == null) throw new Exception($"Job with ID {jobId} not found");
		Arguments? args = JsonSerializer.Deserialize<Arguments>(job.Arguments);
		if (args == null) throw new Exception("Invalid arguments");

		job.Message = "Collecting videos";
		await db.SaveChangesAsync(cancellationToken);
		IIncludableQueryable<DatabaseVideo, DatabaseContent> query = db.Videos
			.Include(x => x.Library)
			.Include(x => x.Episode)
			.ThenInclude(x => x!.ParentContent);
		DatabaseVideo[] videos = args.LibraryId.HasValue
			? await query.Where(v => v.LibraryId == args.LibraryId).ToArrayAsync(cancellationToken)
			: await query.ToArrayAsync(cancellationToken);

		job.Message = "Scanning directories";
		job.ProgressMax = videos.Length;
		int failed = 0;
		for (int i = 0; i < videos.Length; i++)
		{
			DatabaseVideo video = videos[i];
			job.Message = video.Episode != null
				? $"Scanning video {video.Episode.ParentContent.Title} - S{video.Episode.Episode}E{video.Episode.Episode}"
				: $"Scanning video {video.Id}";
			job.Progress = i + 1;
			await db.SaveChangesAsync(cancellationToken);
			try
			{
				string path = Path.Join(video.Library.Path, video.Id.ToString(), "captions");
				if (!Directory.Exists(path))
				{
					video.SubtitleLanguages = "";
				}
				else
				{
					DirectoryInfo dirInfo = new(path);
					video.SubtitleLanguages = string.Join(",", dirInfo.GetFiles("*")
						.Select(x => Path.GetFileNameWithoutExtension(x.Name).Split(".")[0])
						.Distinct());
					tm.RecalculateSystemTagsFor(video);
				}

				await db.SaveChangesAsync(cancellationToken);
			}
			catch (Exception e)
			{
				logger.LogError(e, "Failed to calculate directory size for video {Id}", video.Id);
				failed++;
			}
		}

		job.Message = $"Scanned {videos.Length} videos ({failed} failed)";
		await db.SaveChangesAsync(cancellationToken);
	}

	public class Arguments
	{
		public Guid? LibraryId { get; set; }
	}
}