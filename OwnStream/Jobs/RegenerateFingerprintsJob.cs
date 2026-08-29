using System.Diagnostics;
using System.Text.Json;
using OwnStream.Database;
using OwnStream.Database.Models;
using OwnStream.Services;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Exceptions;

namespace OwnStream.Jobs;

[Job("RegenerateFingerprints")]
public class RegenerateFingerprintsJob : IJob
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

		DatabaseVideo? video = await db.Videos.FindAsync([args.VideoId], cancellationToken);
		if (video == null) throw new Exception("Video not found");
		
		string inputFile = Path.Join(library.Path, video.Id.ToString(), "master.m3u8");
		DirectoryInfo tmpFingerprintsDir = Directory.CreateTempSubdirectory("os_fingerprint_");
		IMediaInfo media = await FFmpeg.GetMediaInfo(inputFile, cancellationToken);
		IVideoStream videoStream = media.VideoStreams.First();

		Conversion conv = new();
		conv.AddParameter("-hide_banner", ParameterPosition.PreInput);
		conv.AddParameter($"-i \"{inputFile}\"");
		conv.AddParameter($"-filter_complex \"[0:v]fps=1,scale=128x128[fingerprint]\"");
		conv.AddParameter($"-map \"[fingerprint]\" \"{Path.Join(tmpFingerprintsDir.FullName, "%07d.png")}\"");

		job.Message = "Transcoding video...";
		await db.SaveChangesAsync(cancellationToken);
		bool dbLock = false;
		Stopwatch sp = new();
		conv.OnDataReceived += async (_, eventArgs) =>
		{
			if (dbLock) return;
			dbLock = true;

			int secs = tmpFingerprintsDir.GetFiles().Length;
			double speed = (secs / DateTimeOffset.UtcNow.Subtract(job.StartedAt ?? new DateTimeOffset()).TotalSeconds);
			job.Message = $"Transcoding at {secs/sp.Elapsed.TotalSeconds:F2} images/sec";

			job.Progress = secs;
			job.ProgressMax = (int)Math.Floor(videoStream.Duration.TotalSeconds);
			job.Status = DatabaseFfmpegJob.JobStatus.Processing;
			await db.SaveChangesAsync(cancellationToken);
			dbLock = false;
		};
		try
		{
			sp.Start();
			await conv.Start(cancellationToken);
			sp.Stop();
		}
		catch (ConversionException e)
		{
			throw new Exception($"Transcode failed.\n$ ffmpeg {conv.Build().Replace(" -", " \\\n\t-")}\n{e.Message}");
		}

		if (dbLock)
			await Task.Delay(1000, cancellationToken);

		job.ProgressMax = null;
		job.Message = "Regenerated fingerprint input files.";
		job.InputPath = tmpFingerprintsDir.FullName;
		await DetectIntroSectionsJob.GenerateFingerprints(job, db,
			Path.Join(library.Path, video.Id.ToString(), DetectIntroSectionsJob.FingerprintsFileName),
			cancellationToken);
		job.Message = "Fingerprint regenerated.";
	}

	public class Arguments
	{
		public Guid VideoId { get; set; }
		public Guid LibraryId { get; set; }
	}
}