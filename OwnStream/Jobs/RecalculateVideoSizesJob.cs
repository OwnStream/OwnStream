using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using OwnStream.Database;
using OwnStream.Database.Models;

namespace OwnStream.Jobs;

[Job("RecalculateVideoSizes")]
public class RecalculateVideoSizesJob : IJob
{
	private DatabaseContext db = null!;
	private ILogger<RecalculateVideoSizesJob> logger = null!;

	public void Initialize(IServiceProvider serviceProvider)
	{
		db = serviceProvider.GetRequiredService<DatabaseContext>();
		logger = serviceProvider.GetRequiredService<ILogger<RecalculateVideoSizesJob>>();
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
				string path = Path.Join(video.Library.Path, video.Id.ToString());
				if (!Directory.Exists(path))
				{
					video.Size = -1;
				}
				else
				{
					DirectoryInfo dirInfo = new(path);
					video.Size = dirInfo.GetFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
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