using System.Text.Json;
using OwnStream.Database;
using OwnStream.Database.Models;
using OwnStream.Services;

namespace OwnStream.Jobs;

[Job("ScanMissingFingerprints")]
public class ScanMissingFingerprintsJob : IJob
{
	private DatabaseContext db = null!;
	private IFfmpegJobQueueService queueService = null!;

	public void Initialize(IServiceProvider serviceProvider)
	{
		db = serviceProvider.GetRequiredService<DatabaseContext>();
		queueService = serviceProvider.GetRequiredService<IFfmpegJobQueueService>();
	}

	public async Task ExecuteJob(Guid jobId, CancellationToken cancellationToken)
	{
		DatabaseFfmpegJob? job = await db.FfmpegJobs.FindAsync([jobId], cancellationToken: cancellationToken);
		if (job == null) throw new Exception($"Job with ID {jobId} not found");
		job.Status = DatabaseFfmpegJob.JobStatus.Processing;
		job.Message = "Initializing";
		Arguments? args = JsonSerializer.Deserialize<Arguments>(job.Arguments);
		if (args == null) throw new Exception("Invalid arguments");
		await db.SaveChangesAsync(cancellationToken);

		DatabaseLibrary? library = await db.Libraries.FindAsync([args.LibraryId], cancellationToken);
		if (library == null) throw new Exception("Library not found");
		
		List<Guid> videosToRefingerprint = [];
		DirectoryInfo dir = new(library.Path);
		DirectoryInfo[] dirs = dir.GetDirectories();
		job.Message = "Scanning directories...";
		job.ProgressMax = dirs.Length;
		job.Progress = 0;
		await db.SaveChangesAsync(cancellationToken);
		foreach (DirectoryInfo videoDir in dirs)
		{
			job.Progress++;
			await db.SaveChangesAsync(cancellationToken);
			if (!Guid.TryParse(videoDir.Name, out Guid videoId)) continue;
			DatabaseVideo? video = await db.Videos.FindAsync([videoId], cancellationToken);
			if (video == null) continue;
			
			string fpPath = Path.Combine(library.Path, videoDir.Name, DetectIntroSectionsJob.FingerprintsFileName);
			if (File.Exists(fpPath) && args.Type == RecreateType.IfDoesntExist) continue;
			videosToRefingerprint.Add(videoId);
		}
		job.Message = $"Will rescan {videosToRefingerprint.Count} files";
		job.ProgressMax = videosToRefingerprint.Count;
		job.Progress = 0;
		await db.SaveChangesAsync(cancellationToken);
		
		foreach (Guid videoId in videosToRefingerprint)
		{
			await queueService.EnqueueAsync("RegenerateFingerprints", "", "", new RegenerateFingerprintsJob.Arguments
			{
				VideoId = videoId,
				LibraryId = library.Id
			});
		}
	}

	public class Arguments
	{
		public RecreateType Type { get; set; }
		public Guid LibraryId { get; set; }
	}

	public enum RecreateType
	{
		All,
		IfDoesntExist
	}
}